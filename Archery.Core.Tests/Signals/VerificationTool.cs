using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using Archery.Core.Angles;
using Archery.Core.Geometry;
using Archery.Core.Landmarks;
using Archery.Core.Signals;
using Xunit;
using Xunit.Abstractions;

namespace Archery.Core.Tests.Signals;

/// <summary>
/// Gerçek videolardan çıkarılmış poz verisi (scripts/extract_pose.py çıktısı) üzerinde atış tespitini doğrular.
/// El tespiti, yumuşatma ve atış doğrulama, web servisindeki (PythonMediaPipeEstimator + AnalysisService) akışla aynıdır.
/// Beklenen atış sayıları videolar tek tek izlenerek belirlenmiştir.
/// </summary>
public class VerificationTool
{
    private readonly ITestOutputHelper _output;
    public VerificationTool(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    // analiz/analizzz uygulamanın ekran kayıtlarıdır (yan yatık, küçük görüntü); sinyalde sırasıyla 3 ve 5 tam çekiş beklemesi var.
    // Eski 4 ve 7 beklentileri ok takma/yay kaldırma hareketlerini de atış sayıyordu.
    [InlineData("analiz_pose.json", 3)]
    [InlineData("analizzz_pose.json", 5)]
    [InlineData("v0_pose.json", 5)]
    [InlineData("v2_pose.json", 5)]  // 3.9 sn: okçu kadraja girerken kol kısa süre kapanıyor — atış değil
    [InlineData("v3_pose.json", 5)]  // 7.2 sn: ok takma — atış değil
    [InlineData("v4_pose.json", 10)] // 9.3 sn: ok takma — atış değil; 115–119 sn: atıştan sonra yay hemen indiriliyor — atış
    [InlineData("v5_pose.json", 3)]  // solak okçu, video tam çekişte başlıyor; 20.8 sn: ok takma — atış değil
    public void VerifyRealVideoShots(string jsonFileName, int expectedShots)
    {
        var shots = DetectShots(jsonFileName);
        if (shots is null) return;

        Assert.Equal(expectedShots, shots.Count);
    }

    [Theory]
    [InlineData("v4_pose.json", 113.0, 120.0)]
    [InlineData("v4_pose.json", 25.0, 31.0)]        // çekme eli beklemenin çoğunda görünmüyor — yine de atış
    [InlineData("analizzz_pose.json", 24.0, 28.0)]  // 24.8 sn'deki tek karelik poz sıçraması bırakma sayılmamalı
    public void VerifyShotPresent(string jsonFileName, double fromSec, double toSec)
    {
        var shots = DetectShots(jsonFileName);
        if (shots is null) return;

        Assert.Contains(shots, s => s.AnchorTime.TotalSeconds >= fromSec && s.ReleaseTime.TotalSeconds <= toSec);
    }

    private IReadOnlyList<ShotSegment>? DetectShots(string jsonFileName)
    {
        string baseDir = AppContext.BaseDirectory;
        string jsonPath = Path.GetFullPath(Path.Combine(baseDir, "../../../TestData", jsonFileName));
        if (!File.Exists(jsonPath))
        {
            _output.WriteLine($"File not found: {jsonPath}");
            return null;
        }

        string json = File.ReadAllText(jsonPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var resultData = JsonSerializer.Deserialize<PoseExtractionResult>(json, options);

        if (resultData?.Frames == null) return null;

        // El tespiti: PythonMediaPipeEstimator ile aynı (yalnız güvenilir noktalar, ortalama omuz-bilek mesafesi)
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
        bool isLeftHanded = rightArmExt > leftArmExt;
        _output.WriteLine($"Auto-detected handedness: {(isLeftHanded ? "Sol Elli" : "Sağ Elli")}");

        var mapping = new Dictionary<string, string>
        {
            { "BowShoulder", isLeftHanded ? "12" : "11" },
            { "BowElbow",    isLeftHanded ? "14" : "13" },
            { "BowWrist",    isLeftHanded ? "16" : "15" },
            { "DrawShoulder",isLeftHanded ? "11" : "12" },
            { "DrawElbow",   isLeftHanded ? "13" : "14" },
            { "DrawWrist",   isLeftHanded ? "15" : "16" }
        };

        var drawArmSeriesRaw = new List<TimedSample>();
        var bowArmSeriesRaw = new List<TimedSample>();

        foreach (var frameData in resultData.Frames)
        {
            var points = new Dictionary<string, LandmarkPoint>();
            foreach (var (domainName, mpIndex) in mapping)
            {
                if (frameData.Landmarks.TryGetValue(mpIndex, out var mpPoint) && mpPoint.Confidence >= 0.5)
                {
                    points[domainName] = new LandmarkPoint(new Point2D(mpPoint.X, mpPoint.Y), mpPoint.Confidence);
                }
            }
            var frame = new LandmarkFrame(TimeSpan.FromSeconds(frameData.TimestampSec), points);
            var results = FrameAngleComputer.ComputeAngles(frame, WellKnownAngles.All);

            double? drawArm = results.FirstOrDefault(r => r.Definition.Name == "DrawArmElbow")?.Degrees;
            double? bowArm = results.FirstOrDefault(r => r.Definition.Name == "BowArmElbow")?.Degrees;

            drawArmSeriesRaw.Add(new TimedSample(frame.Timestamp, drawArm));
            bowArmSeriesRaw.Add(new TimedSample(frame.Timestamp, bowArm));
        }

        // AnalysisService ile aynı: yumuşatma + [0, 180] kırpma
        var segmentationOptions = new ShotSegmentationOptions();
        static IReadOnlyList<TimedSample> Clamp(IReadOnlyList<TimedSample> series) =>
            series.Select(s => s.Value.HasValue ? s with { Value = Math.Clamp(s.Value.Value, 0.0, 180.0) } : s).ToList();

        var drawArmSeries = Clamp(SavitzkyGolayFilter.Apply(drawArmSeriesRaw, segmentationOptions.SmoothingWindowSamples, segmentationOptions.SmoothingPolynomialOrder));
        var bowArmSeries = Clamp(SavitzkyGolayFilter.Apply(bowArmSeriesRaw, segmentationOptions.SmoothingWindowSamples, segmentationOptions.SmoothingPolynomialOrder));

        var shots = ShotDetectionPipeline.Detect(drawArmSeries, bowArmSeries, segmentationOptions, _output.WriteLine);

        _output.WriteLine($"Total valid shots found: {shots.Count}");
        foreach (var s in shots)
        {
            _output.WriteLine($"Shot: Draw={s.DrawStart}, Anchor={s.AnchorTime}, Release={s.ReleaseTime}, End={s.End}");
        }

        return shots;
    }

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
