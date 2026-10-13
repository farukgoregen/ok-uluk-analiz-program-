namespace Archery.Core.Signals;

/// <summary>
/// Atıştan hemen sonra yayını indiren (let-down) kişilerde bırakma anını düzeltir.
/// Bu durumda çekme kolu bırakmadan sonra da düşük açıda kalır; segmentasyon minimum açıyı geç bulur ve
/// analiz penceresine (tam çekiş → bırakma) kol çöküşü ve yayın indirilmesi girer.
///
/// Düzeltme yalnızca yay kolu (BowArm) bırakmadan hemen önce gerçekten çöktüyse uygulanır;
/// yay kolu dik kalan normal atışlara dokunulmaz.
/// </summary>
public static class ShotReleaseRefiner
{
    public static ShotSegment Refine(
        ShotSegment shot,
        IReadOnlyList<TimedSample> drawArmSeries,
        IReadOnlyList<TimedSample> bowArmSeries,
        ShotSegmentationOptions options)
    {
        ArgumentNullException.ThrowIfNull(shot);
        ArgumentNullException.ThrowIfNull(drawArmSeries);
        ArgumentNullException.ThrowIfNull(bowArmSeries);
        ArgumentNullException.ThrowIfNull(options);

        var bow = Smooth(bowArmSeries, options);
        var windowStart = shot.ReleaseTime - TimeSpan.FromSeconds(options.BowLetDownWindowSec);

        bool letDown = bow.Any(s => s.Timestamp >= windowStart && s.Timestamp <= shot.ReleaseTime
                                    && s.Value < options.BowLetDownAngleDeg);
        if (!letDown)
            return shot;

        var draw = Smooth(drawArmSeries, options)
            .Where(s => s.Timestamp >= shot.AnchorTime && s.Timestamp <= shot.ReleaseTime)
            .ToList();

        int count = draw.Count;
        if (count < 4)
            return shot;

        // Bekleme platosu: tam çekişten sonraki ilk yarının medyan açısı
        var firstHalf = draw.Take(count / 2).Select(s => s.Value).OrderBy(v => v).ToList();
        double plateau = firstHalf[firstHalf.Count / 2];

        for (int k = count / 2; k < count; k++)
        {
            if (draw[k].Value >= plateau - options.ReleaseCollapseDeg)
                continue;

            int start = k;
            while (start > 0 && draw[start].Value < plateau)
                start--;

            var refined = draw[start].Timestamp;
            return (shot.ReleaseTime - refined).TotalSeconds >= options.ReleaseCollapseMinAdvanceSec
                ? shot with { ReleaseTime = refined }
                : shot;
        }

        return shot;
    }

    // Segmentasyon ile aynı yumuşatma: boş değerler atılır, Savitzky-Golay uygulanır
    private static List<(TimeSpan Timestamp, double Value)> Smooth(
        IReadOnlyList<TimedSample> series,
        ShotSegmentationOptions options)
    {
        var valid = series.Where(s => s.Value.HasValue).ToList();
        if (valid.Count < options.SmoothingWindowSamples)
            return new List<(TimeSpan, double)>();

        return SavitzkyGolayFilter.Apply(valid, options.SmoothingWindowSamples, options.SmoothingPolynomialOrder)
            .Where(s => s.Value.HasValue)
            .Select(s => (s.Timestamp, s.Value!.Value))
            .ToList();
    }
}
