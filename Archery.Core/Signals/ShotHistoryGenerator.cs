namespace Archery.Core.Signals;

public record PanelSnapshot(
    int ShotIndex,
    double ReleaseTimestamp,
    List<ShotRecord> Rows,
    ShotRecord? Ort
);

public record ShotHistoryResult(
    List<PanelSnapshot> Snapshots
);

public record ShotRecord(
    int ShotNumber,
    bool IsReference,
    double? SimilarityPercent,
    IReadOnlyList<double> CurveSamples,
    double? StartTimeSec = null,
    double? EndTimeSec = null,
    bool HasDataGap = false,
    double MaxDataGapSec = 0,
    IReadOnlyList<double>? ProgressiveSimilarity = null,
    double? DurationSec = null
);

/// <summary>
/// Açı serisini alır, atışları böler, normalize eder, referansı seçer ve benzerlikleri hesaplar.
/// Uçtan uca atış geçmişi (shot history) panelinin hesaplama motorudur.
/// </summary>
public class ShotHistoryGenerator
{
    /// <summary>
    /// Analiz penceresinde (AnchorTime → ReleaseTime) bu süreden uzun ölçümsüz aralık varsa atış "eksik veri" olarak işaretlenir.
    /// Bu aralıklar eğride lineer interpolasyonla doldurulur, yani gerçek ölçüm değildir.
    /// </summary>
    public const double DataGapWarningSec = 0.5;

    public ShotHistoryResult Analyze(IReadOnlyList<TimedSample> series)
    {
        var shots = ShotSegmenterStateMachine.DetectShots(series, new ShotSegmentationOptions());
        return AnalyzeWithSegments(series, shots);
    }

    public ShotHistoryResult AnalyzeWithExternalSegments(
        IReadOnlyList<TimedSample> series,
        IReadOnlyList<ShotSegment> externalShots)
    {
        return AnalyzeWithSegments(series, externalShots);
    }

    public static IReadOnlyList<ShotSegment> DetectShotsFromDrawArm(IReadOnlyList<TimedSample> drawArmSeries)
    {
        return ShotSegmenterStateMachine.DetectShots(drawArmSeries, new ShotSegmentationOptions());
    }

    private ShotHistoryResult AnalyzeWithSegments(
        IReadOnlyList<TimedSample> series,
        IReadOnlyList<ShotSegment> shots)
    {
        if (shots.Count == 0)
        {
            return new ShotHistoryResult(new List<PanelSnapshot>());
        }

        var curves = new List<NormalizedCurve>();
        var validShots = new List<ShotSegment>();
        var maxGaps = new List<double>();

        foreach (var shot in shots)
        {
            var curve = CurveNormalizer.NormalizeShot(series, shot);
            if (curve != null)
            {
                curves.Add(curve);
                validShots.Add(shot);
                maxGaps.Add(MaxDataGapSeconds(series, shot));
            }
        }

        var snapshots = new List<PanelSnapshot>();

        if (curves.Count == 0)
        {
            return new ShotHistoryResult(snapshots);
        }

        // HER ATIS ICIN ANLIK DURUM (SNAPSHOT) HESAPLA
        for (int k = 1; k <= curves.Count; k++)
        {
            var currentCurves = curves.Take(k).ToList();
            var currentValidShots = validShots.Take(k).ToList();

            var averageCurve = CurveAverager.Average(currentCurves);
            if (averageCurve == null) continue;

            var records = new List<ShotRecord>();
            double sumScore = 0;
            double sumDuration = 0;
            int scoredCount = 0;

            for (int i = 0; i < currentCurves.Count; i++)
            {
                // Atış süresi: tam çekiş → bırakma (video zaman damgası, PTS)
                double duration = (currentValidShots[i].ReleaseTime - currentValidShots[i].AnchorTime).TotalSeconds;
                sumDuration += duration;
                bool isRef = false;
                double? similarity = null;
                IReadOnlyList<double>? progressive = null;

                if (currentCurves.Count == 1)
                {
                    isRef = true;
                }
                else
                {
                    var score = CurveComparer.Compare(averageCurve, currentCurves[i]);
                    similarity = score.SimilarityPercent;
                    progressive = ProgressiveSimilarity(averageCurve, currentCurves[i]);
                    sumScore += similarity.Value;
                    scoredCount++;
                }

                records.Add(new ShotRecord(
                    // Numara atışın videodaki sırasıdır: bir kolda veri olmadığı için düşen atışlar
                    // numaralamayı kaydırmaz, iki paneldeki aynı numara aynı atışı gösterir.
                    ShotNumber: currentValidShots[i].ShotIndex + 1,
                    IsReference: isRef,
                    SimilarityPercent: similarity,
                    CurveSamples: currentCurves[i].Samples,
                    StartTimeSec: currentValidShots[i].AnchorTime.TotalSeconds,
                    EndTimeSec: currentValidShots[i].ReleaseTime.TotalSeconds,
                    HasDataGap: maxGaps[i] > DataGapWarningSec,
                    MaxDataGapSec: maxGaps[i],
                    ProgressiveSimilarity: progressive,
                    DurationSec: duration
                ));
            }

            ShotRecord? averageRecord = new ShotRecord(
                ShotNumber: 0,
                IsReference: false,
                SimilarityPercent: scoredCount > 0 ? sumScore / scoredCount : null,
                CurveSamples: averageCurve.Samples,
                StartTimeSec: null,
                EndTimeSec: null,
                DurationSec: sumDuration / currentCurves.Count
            );

            double releaseTimestamp = currentValidShots[k - 1].ReleaseTime.TotalSeconds;

            snapshots.Add(new PanelSnapshot(
                ShotIndex: k,
                ReleaseTimestamp: releaseTimestamp,
                Rows: records,
                Ort: averageRecord
            ));
        }

        return new ShotHistoryResult(snapshots);
    }

    /// <summary>
    /// Atış ilerledikçe o ana kadarki benzerlik: k. eleman, iki eğrinin ilk k+1 örneğinin karşılaştırmasıdır.
    /// Son eleman atışın nihai benzerliğine eşittir. Arayüzde video oynarken canlı gösterim için kullanılır;
    /// böylece tarayıcı benzerliği yeniden hesaplamaz, sunucudaki değeri gösterir.
    /// </summary>
    public static IReadOnlyList<double> ProgressiveSimilarity(NormalizedCurve reference, NormalizedCurve target)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(target);

        int count = Math.Min(reference.Samples.Count, target.Samples.Count);
        var result = new double[count];
        for (int k = 0; k < count; k++)
        {
            var refPrefix = new NormalizedCurve(reference.Samples.Take(k + 1).ToArray());
            var targetPrefix = new NormalizedCurve(target.Samples.Take(k + 1).ToArray());
            result[k] = CurveComparer.Compare(refPrefix, targetPrefix).SimilarityPercent;
        }
        return result;
    }

    /// <summary>
    /// Atışın analiz penceresinde (AnchorTime → ReleaseTime) ölçüm alınamayan en uzun aralığı (saniye) döndürür.
    /// Pencere başı/sonu ile ilk/son geçerli ölçüm arasındaki süreler de boşluk sayılır.
    /// </summary>
    public static double MaxDataGapSeconds(IReadOnlyList<TimedSample> series, ShotSegment shot)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(shot);

        TimeSpan previous = shot.AnchorTime;
        TimeSpan maxGap = TimeSpan.Zero;

        foreach (var sample in series)
        {
            if (sample.Timestamp < shot.AnchorTime || sample.Timestamp > shot.ReleaseTime || !sample.Value.HasValue)
                continue;

            if (sample.Timestamp - previous > maxGap)
                maxGap = sample.Timestamp - previous;
            previous = sample.Timestamp;
        }

        if (shot.ReleaseTime - previous > maxGap)
            maxGap = shot.ReleaseTime - previous;

        return maxGap.TotalSeconds;
    }
}
