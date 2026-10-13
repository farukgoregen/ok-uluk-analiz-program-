namespace Archery.Core.Signals;

/// <summary>
/// Atış tespitinin uçtan uca akışı: çekme kolu serisinden aday atışlar, bırakma anı düzeltmesi,
/// yay kolu ve bekleme süresi doğrulaması, yeniden numaralandırma.
/// Web servisi ve gerçek video testleri aynı akışı kullanır; böylece testler üretimdeki davranışı ölçer.
/// Giriş serileri yumuşatılmış (Savitzky-Golay) ve [0, 180] aralığına kırpılmış olmalıdır.
/// </summary>
public static class ShotDetectionPipeline
{
    public static IReadOnlyList<ShotSegment> Detect(
        IReadOnlyList<TimedSample> drawArmSeries,
        IReadOnlyList<TimedSample> bowArmSeries,
        ShotSegmentationOptions? options = null,
        Action<string>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(drawArmSeries);
        ArgumentNullException.ThrowIfNull(bowArmSeries);
        options ??= new ShotSegmentationOptions();

        // Durum makinesinin kare kare izleme çıktısı loga verilmez; yalnızca atış kararları raporlanır
        var candidates = ShotSegmenterStateMachine.DetectShots(drawArmSeries, options);
        var shots = new List<ShotSegment>();

        foreach (var candidate in candidates)
        {
            // Bırakma anı önce düzeltilir: atıştan hemen sonra yayı indiren kişilerde ham pencere
            // yay kolunun çöküşünü de içerir ve aşağıdaki yay kolu kontrolü gerçek atışı reddederdi.
            var shot = ShotReleaseRefiner.Refine(candidate, drawArmSeries, bowArmSeries, options);

            double holdSec = (shot.ReleaseTime - shot.AnchorTime).TotalSeconds;
            if (holdSec < options.MinHoldDurationSec)
            {
                logger?.Invoke($"Atış reddedildi ({shot.AnchorTime.TotalSeconds:F2} sn): bekleme çok kısa ({holdSec:F2} sn)");
                continue;
            }

            // Okçu kadrajdan çıkınca poz kaybolur ve uzun bir ölçümsüz boşluk "bekleme" gibi görünebilir;
            // beklemenin bir kısmı gerçekten ölçülmüş olmalıdır.
            double measuredSec = MeasuredSeconds(drawArmSeries, shot.AnchorTime, shot.ReleaseTime);
            if (measuredSec < options.MinMeasuredHoldSec)
            {
                logger?.Invoke($"Atış reddedildi ({shot.AnchorTime.TotalSeconds:F2} sn): beklemenin yalnızca {measuredSec:F2} sn'si ölçülmüş");
                continue;
            }

            var bowDuringHold = bowArmSeries
                .Where(b => b.Timestamp >= shot.AnchorTime && b.Timestamp <= shot.ReleaseTime && b.Value.HasValue)
                .Select(b => b.Value!.Value)
                .ToList();

            // Yay kolu hiç görünmüyorsa bu kontrol yapılamaz; atış çekme kolu kriterleriyle kabul edilir.
            if (bowDuringHold.Count > 0 && bowDuringHold.Average() < options.MinBowArmHoldAngleDeg)
            {
                logger?.Invoke($"Atış reddedildi ({shot.AnchorTime.TotalSeconds:F2} sn): yay kolu bükük ({bowDuringHold.Average():F1}°)");
                continue;
            }

            shots.Add(shot with { ShotIndex = shots.Count });
        }

        return shots;
    }

    /// <summary>
    /// [from, to] aralığının ölçümle kapsanan süresi (saniye): ardışık geçerli örnekler arasındaki aralıklar toplanır,
    /// <see cref="ShotHistoryGenerator.DataGapWarningSec"/>'ten uzun aralıklar ölçümsüz boşluk sayılıp atlanır.
    /// </summary>
    public static double MeasuredSeconds(IReadOnlyList<TimedSample> series, TimeSpan from, TimeSpan to)
    {
        ArgumentNullException.ThrowIfNull(series);

        double total = 0;
        TimeSpan? previous = null;
        foreach (var sample in series)
        {
            if (sample.Timestamp < from || sample.Timestamp > to || !sample.Value.HasValue)
                continue;

            if (previous.HasValue)
            {
                double dt = (sample.Timestamp - previous.Value).TotalSeconds;
                if (dt <= ShotHistoryGenerator.DataGapWarningSec)
                    total += dt;
            }
            previous = sample.Timestamp;
        }
        return total;
    }
}
