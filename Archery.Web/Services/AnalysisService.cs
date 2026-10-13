using Archery.Core.Angles;
using Archery.Web.Models;

namespace Archery.Web.Services;

/// <summary>
/// Tum analiz is akisini yoneten servis.
/// </summary>
public class AnalysisService
{
    private readonly ISessionStore _sessionStore;
    private readonly IPoseEstimator _poseEstimator;
    private readonly ILogger<AnalysisService> _logger;

    public AnalysisService(
        ISessionStore sessionStore,
        IPoseEstimator poseEstimator,
        ILogger<AnalysisService> logger)
    {
        _sessionStore = sessionStore;
        _poseEstimator = poseEstimator;
        _logger = logger;
    }

    /// <summary>
    /// Verilen oturum icin arka planda analizi calistirir.
    /// </summary>
    public async Task RunAnalysisAsync(Guid sessionId, string videoFilePath)
    {
        try
        {
            var session = await _sessionStore.GetAsync(sessionId);
            if (session is null) return;

            session.Status = SessionStatus.Analyzing;
            await _sessionStore.UpdateAsync(session);

            _logger.LogInformation("Analiz baslatildi. Oturum: {SessionId}", sessionId);

            // Python (MediaPipe) ile video isleyip LandmarkFrame listesi al
            var (frames, detectedFps) = await _poseEstimator.ExtractFramesAsync(videoFilePath);

            // Acilari hesapla ve DTO'ya donustur
            var frameDataList = new List<FrameData>();
            var definitions = WellKnownAngles.All;

            foreach (var frame in frames)
            {
                var results = FrameAngleComputer.ComputeAngles(frame, definitions);

                var anglesDict = new Dictionary<string, double?>();
                foreach (var result in results)
                {
                    anglesDict[result.Definition.Name] = result.Degrees;
                }

                var keypointsDict = frame.Points.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Position);
                frameDataList.Add(new FrameData(frame.Timestamp.TotalSeconds, keypointsDict, anglesDict));
            }

            var analysisResult = new SessionAnalysis(sessionId, detectedFps, frameDataList);

            // Atis gecmisi analizi
            try
            {
                var historyGenerator = new Archery.Core.Signals.ShotHistoryGenerator();

                // DrawArm sinyali (atislar bu seri uzerinden tespit edilir — cekis/birakma kinematiği)
                var drawArmSeriesRaw = frameDataList
                    .Select(f => new Archery.Core.Signals.TimedSample(
                        TimeSpan.FromSeconds(f.TimestampSec),
                        f.Angles.GetValueOrDefault("DrawArmElbow")))
                    .ToList();

                // BowArm sinyali (atis zamanlari DrawArm'dan alinir — yay kolu spike uretmez)
                var bowArmSeriesRaw = frameDataList
                    .Select(f => new Archery.Core.Signals.TimedSample(
                        TimeSpan.FromSeconds(f.TimestampSec),
                        f.Angles.GetValueOrDefault("BowArmElbow")))
                    .ToList();

                // Savitzky-Golay filtresi uygula — hem UI hem atış tespiti aynı filtrelenmiş seriden beslenir
                var smoothingOptions = new Archery.Core.Signals.ShotSegmentationOptions();
                var drawArmSeries = Archery.Core.Signals.SavitzkyGolayFilter.Apply(
                    drawArmSeriesRaw,
                    smoothingOptions.SmoothingWindowSamples,
                    smoothingOptions.SmoothingPolynomialOrder);

                var bowArmSeries = Archery.Core.Signals.SavitzkyGolayFilter.Apply(
                    bowArmSeriesRaw,
                    smoothingOptions.SmoothingWindowSamples,
                    smoothingOptions.SmoothingPolynomialOrder);

                // SG filtresi keskin geçişlerde aşma (overshoot) yapabilir; dirsek açısı tanım gereği [0, 180] aralığındadır
                static IReadOnlyList<Archery.Core.Signals.TimedSample> ClampToAngleRange(IReadOnlyList<Archery.Core.Signals.TimedSample> series) =>
                    series.Select(s => s.Value.HasValue ? s with { Value = Math.Clamp(s.Value.Value, 0.0, 180.0) } : s).ToList();

                drawArmSeries = ClampToAngleRange(drawArmSeries);
                bowArmSeries = ClampToAngleRange(bowArmSeries);

                // Atış tespiti: aday atışlar DrawArm'dan; bırakma düzeltmesi, bekleme süresi ve yay kolu doğrulaması
                // Core'daki ortak akışta yapılır (gerçek video testleri de aynı akışı kullanır).
                var drawArmShots = Archery.Core.Signals.ShotDetectionPipeline.Detect(
                    drawArmSeries, bowArmSeries, smoothingOptions, msg => _logger.LogInformation("{Message}", msg));

                _logger.LogInformation("Gecerli atis sayisi: {Count}", drawArmShots.Count);
                foreach (var s in drawArmShots)
                {
                    _logger.LogInformation($"Shot #{s.ShotIndex}: Draw={s.DrawStart}, Anchor={s.AnchorTime}, Release={s.ReleaseTime}, End={s.End}");
                }

                // DrawArm analizi: segmentler DrawArm'dan
                var drawArmHistory = historyGenerator.AnalyzeWithExternalSegments(drawArmSeries, drawArmShots);

                // BowArm analizi: ayni atis zaman pencereleri, BowArm acilari
                var bowArmHistory = historyGenerator.AnalyzeWithExternalSegments(bowArmSeries, drawArmShots);

                ShotRecordDto? MapRecord(Archery.Core.Signals.ShotRecord? r) =>
                    r == null ? null : new ShotRecordDto(r.ShotNumber, r.IsReference, r.SimilarityPercent, r.CurveSamples, r.StartTimeSec, r.EndTimeSec, r.HasDataGap, r.MaxDataGapSec, r.ProgressiveSimilarity, r.DurationSec);

                PanelSnapshotDto MapSnapshot(Archery.Core.Signals.PanelSnapshot s) =>
                    new PanelSnapshotDto(s.ShotIndex, s.ReleaseTimestamp, s.Rows.Select(MapRecord).ToList()!, MapRecord(s.Ort));

                var historyDto = new ShotHistoryDto(
                    BowArmSnapshots: bowArmHistory.Snapshots.Select(MapSnapshot).ToList(),
                    DrawArmSnapshots: drawArmHistory.Snapshots.Select(MapSnapshot).ToList()
                );

                analysisResult = analysisResult with { History = historyDto };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Atis gecmisi analizi olusturulurken hata: {SessionId}", sessionId);
                // Atis analizi patlasa bile genel analiz sonucunu kaydedelim
            }

            await _sessionStore.SaveAnalysisAsync(analysisResult);

            session.Status = SessionStatus.Completed;
            await _sessionStore.UpdateAsync(session);

            _logger.LogInformation("Analiz tamamlandi. Oturum: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analiz sirasinda hata olustu. Oturum: {SessionId}", sessionId);
            var session = await _sessionStore.GetAsync(sessionId);
            if (session is not null)
            {
                session.Status = SessionStatus.Failed;
                await _sessionStore.UpdateAsync(session);
            }
        }
    }
}
