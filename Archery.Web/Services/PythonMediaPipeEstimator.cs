using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Archery.Core.Geometry;
using Archery.Core.Landmarks;

namespace Archery.Web.Services;

public class PythonMediaPipeEstimator : IPoseEstimator
{
    private readonly ILogger<PythonMediaPipeEstimator> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public PythonMediaPipeEstimator(ILogger<PythonMediaPipeEstimator> logger, IWebHostEnvironment env, IConfiguration configuration)
    {
        _logger = logger;
        _env = env;
        _configuration = configuration;
    }

    /// <summary>
    /// Kullanılacak Python yorumlayıcısı. appsettings'teki "PythonPath" ayarı verilmiş ve dosya mevcutsa o kullanılır;
    /// aksi halde Windows'ta "python", Linux/WSL'de "python3" kullanılır.
    /// Böylece aynı ayar dosyası hem Windows'ta hem WSL'de çalışır.
    /// </summary>
    private string ResolvePythonExecutable()
    {
        var configured = _configuration["PythonPath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (File.Exists(configured))
                return configured;

            _logger.LogWarning("PythonPath ayarındaki yol bulunamadı: {PythonPath}. Sistem varsayılanı kullanılacak.", configured);
        }

        return OperatingSystem.IsWindows() ? "python" : "python3";
    }

    public async Task<(List<LandmarkFrame> Frames, double Fps)> ExtractFramesAsync(string videoPath)
    {
        var frames = new List<LandmarkFrame>();
        double detectedFps = 30.0;

        // Geçici bir JSON dosyası oluştur
        string tempJsonPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        
        try
        {
            // Python scriptinin yolu (kök dizinde scripts klasöründe)
            // _env.ContentRootPath Archery.Web dizinidir. Bizim scripts klasörü kök dizinde (okçuluk/scripts).
            var projectRoot = Directory.GetParent(_env.ContentRootPath)?.FullName ?? _env.ContentRootPath;
            var scriptPath = Path.Combine(projectRoot, "scripts", "extract_pose.py");

            var pythonExecutable = ResolvePythonExecutable();

            _logger.LogInformation("Python scripti başlatılıyor: {Python} {ScriptPath} - Video: {VideoPath}", pythonExecutable, scriptPath, videoPath);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = $"\"{scriptPath}\" \"{videoPath}\" \"{tempJsonPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Python süreci başlatılamadı.");
            }

            // Çıktıları oku (hata ayıklama için)
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError("Python scripti hata ile döndü. Çıkış kodu: {ExitCode}\nHata: {Error}\nÇıktı: {Output}", process.ExitCode, error, output);
                throw new InvalidOperationException($"Python analiz hatası: {error}");
            }

            _logger.LogInformation("Python analizi tamamlandı. JSON okunuyor...");

            // JSON dosyasını oku
            var jsonContent = await File.ReadAllTextAsync(tempJsonPath);

            // JSON'ı deserialize et
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var resultData = JsonSerializer.Deserialize<PoseExtractionResult>(jsonContent, options);

            if (resultData?.Frames == null)
                return (frames, detectedFps);

            // Python'dan gelen gerçek FPS'yi al
            if (resultData.Fps > 0)
                detectedFps = resultData.Fps;

            // Sol ve sağ kolun ortalama açıklığını (omuz ile bilek arası mesafe) hesapla.
            // Toplam yerine ortalama kullanılır: bir kol daha az karede görünüyorsa toplamı yapay olarak küçük çıkar.
            // Sadece güvenilir (>= 0.5) noktalar sayılır.
            double leftArmSum = 0, rightArmSum = 0;
            int leftArmCount = 0, rightArmCount = 0;

            foreach (var frame in resultData.Frames)
            {
                if (frame.Landmarks.TryGetValue("11", out var ls) && frame.Landmarks.TryGetValue("15", out var lw)
                    && ls.Confidence >= 0.5 && lw.Confidence >= 0.5)
                {
                    leftArmSum += Math.Sqrt(Math.Pow(ls.X - lw.X, 2) + Math.Pow(ls.Y - lw.Y, 2));
                    leftArmCount++;
                }

                if (frame.Landmarks.TryGetValue("12", out var rs) && frame.Landmarks.TryGetValue("16", out var rw)
                    && rs.Confidence >= 0.5 && rw.Confidence >= 0.5)
                {
                    rightArmSum += Math.Sqrt(Math.Pow(rs.X - rw.X, 2) + Math.Pow(rs.Y - rw.Y, 2));
                    rightArmCount++;
                }
            }

            double leftArmExt = leftArmCount > 0 ? leftArmSum / leftArmCount : 0;
            double rightArmExt = rightArmCount > 0 ? rightArmSum / rightArmCount : 0;

            // Yay tutan kol daha gergin ve düz olduğu için omuzdan bileğe olan mesafe daha fazladır.
            // Sağ kol daha açıksa, sağ kol yay koludur -> Okçu Solaktır (isLeftHanded = true).
            bool isLeftHanded = rightArmExt > leftArmExt;
            _logger.LogInformation("Auto-detected handedness: {Handedness} (Left Ext: {Left}, Right Ext: {Right})", 
                isLeftHanded ? "Sol Elli" : "Sağ Elli", leftArmExt, rightArmExt);

            var mapping = new Dictionary<string, string>
            {
                { "BowShoulder", isLeftHanded ? "12" : "11" },
                { "BowElbow",    isLeftHanded ? "14" : "13" },
                { "BowWrist",    isLeftHanded ? "16" : "15" },
                { "DrawShoulder",isLeftHanded ? "11" : "12" },
                { "DrawElbow",   isLeftHanded ? "13" : "14" },
                { "DrawWrist",   isLeftHanded ? "15" : "16" }
            };

            foreach (var frameData in resultData.Frames)
            {
                var points = new Dictionary<string, LandmarkPoint>();

                foreach (var (domainName, mpIndex) in mapping)
                {
                    if (frameData.Landmarks.TryGetValue(mpIndex, out var mpPoint) && mpPoint.Confidence >= 0.5)
                    {
                        var point2D = new Point2D(mpPoint.X, mpPoint.Y);
                        points[domainName] = new LandmarkPoint(point2D, mpPoint.Confidence);
                    }
                }

                frames.Add(new LandmarkFrame(TimeSpan.FromSeconds(frameData.TimestampSec), points));
            }
        }
        finally
        {
            // Geçici dosyayı temizle
            if (File.Exists(tempJsonPath))
            {
                File.Delete(tempJsonPath);
            }
        }

        return (frames, detectedFps);
    }

    // JSON'dan okunacak geçici sınıflar
    private class PoseExtractionResult
    {
        public double Fps { get; set; }
        public List<PoseFrameData>? Frames { get; set; }
    }

    private class PoseFrameData
    {
        public double TimestampSec { get; set; }
        public Dictionary<string, PoseLandmarkData> Landmarks { get; set; } = new();
    }

    private class PoseLandmarkData
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Confidence { get; set; }
    }
}
