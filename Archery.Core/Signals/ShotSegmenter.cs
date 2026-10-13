namespace Archery.Core.Signals;

/// <summary>
/// DrawArm dirsek açısı serisinden atışları tespit eden segmentasyon motoru.
///
/// Kinematik model:
/// 1. Çekiş fazı: Açı sürekli azalır (kol bükülür, ~170° → ~30°).
/// 2. Anchor/tam çekiş: Açı bir süre platoda kalır.
/// 3. Bırakma: Açı ani ve hızlı sıçrar (ipi bırakma, ~30° → ~170°).
///
/// Strateji: Açı serisinde "yüksek → düşük" geçişlerini bul ve her birini
/// geometrik kriterlerle doğrula. Türev tabanlı spike tespiti YERİNE
/// açısal eşikler ve zaman pencereleri kullanılır.
/// </summary>
public static class ShotSegmenter
{
    /// <summary>
    /// Zaman damgalı açı serisinden atış segmentlerini tespit eder.
    /// </summary>
    /// <param name="angleSeries">DrawArm dirsek açısının zaman serisi.</param>
    /// <param name="options">Segmentasyon parametreleri. null ise varsayılan değerler kullanılır.</param>
    /// <returns>Tespit edilen atışların listesi, kronolojik sırada.</returns>
    public static IReadOnlyList<ShotSegment> DetectShots(
        IReadOnlyList<TimedSample> angleSeries,
        ShotSegmentationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(angleSeries);
        options ??= new ShotSegmentationOptions();

        // 1. Null olmayan örnekleri filtrele, orijinal indekslerini koru
        var validSamples = new List<(int OriginalIndex, TimeSpan Timestamp, double Value)>();
        for (int i = 0; i < angleSeries.Count; i++)
        {
            var sample = angleSeries[i];
            if (sample.Value.HasValue)
            {
                validSamples.Add((i, sample.Timestamp, sample.Value.Value));
            }
        }

        if (validSamples.Count < 3)
            return Array.Empty<ShotSegment>();

        // 2. Savitzky-Golay filtresi uygula (sıfır faz, gecikmesiz)
        var smoothed = ApplySavitzkyGolay(validSamples, options);

        // 3. Yerel maksimum ve minimum noktalarını bul
        var (maxima, minima) = FindLocalExtrema(smoothed);

        // 4. Her minimum (anchor) için geriye dönük en yakın maksimumu (draw start) bul
        var shots = new List<ShotSegment>();
        int shotIndex = 0;
        TimeSpan? lastShotTime = null;

        foreach (var minIdx in minima)
        {
            // Minimum değer anchor eşiğinin altında olmalı
            if (smoothed[minIdx].Value > options.AnchorClosedAngleThreshold)
                continue;

            // Refrakter süre kontrolü
            if (lastShotTime.HasValue)
            {
                double timeSinceLastShot = (smoothed[minIdx].Timestamp - lastShotTime.Value).TotalSeconds;
                if (timeSinceLastShot < options.RefractoryPeriodSec)
                    continue;
            }

            // Geriye dönük en yakın geçerli maksimumu bul (DrawOpenAngleThreshold üzerinde olan)
            int? bestMaxIdx = null;
            for (int i = maxima.Count - 1; i >= 0; i--)
            {
                int maxIdx = maxima[i];
                if (maxIdx < minIdx && smoothed[maxIdx].Value >= options.DrawOpenAngleThreshold)
                {
                    bestMaxIdx = maxIdx;
                    break; // En yakın olanı alıyoruz
                }
            }

            if (bestMaxIdx is null)
                continue;

            // Geometrik doğrulama
            var segment = ValidateAndBuildSegment(smoothed, bestMaxIdx.Value, minIdx, shotIndex, options);
            if (segment is not null)
            {
                shots.Add(segment);
                lastShotTime = segment.End; // Atış tamamen bittikten sonra refrakter süreyi başlat
                shotIndex++;
            }
        }

        return shots;
    }

    /// <summary>
    /// Savitzky-Golay filtresi uygular.
    /// </summary>
    private static List<(TimeSpan Timestamp, double Value)> ApplySavitzkyGolay(
        List<(int OriginalIndex, TimeSpan Timestamp, double Value)> samples,
        ShotSegmentationOptions options)
    {
        var timedSamples = samples
            .Select(s => new TimedSample(s.Timestamp, s.Value))
            .ToList();

        var filtered = SavitzkyGolayFilter.Apply(
            timedSamples,
            options.SmoothingWindowSamples,
            options.SmoothingPolynomialOrder);

        return filtered
            .Select(s => (s.Timestamp, s.Value!.Value))
            .ToList();
    }

    /// <summary>
    /// Yerel maksimum ve minimum noktalarını bulur.
    /// </summary>
    private static (List<int> Maxima, List<int> Minima) FindLocalExtrema(
        List<(TimeSpan Timestamp, double Value)> smoothed)
    {
        var maxima = new List<int>();
        var minima = new List<int>();

        for (int i = 1; i < smoothed.Count - 1; i++)
        {
            double prev = smoothed[i - 1].Value;
            double curr = smoothed[i].Value;
            double next = smoothed[i + 1].Value;

            // Yerel maksimum
            if (curr > prev && curr > next)
            {
                maxima.Add(i);
            }
            // Yerel minimum
            else if (curr < prev && curr < next)
            {
                minima.Add(i);
            }
        }

        return (maxima, minima);
    }

    /// <summary>
    /// Maksimum-minimum çiftini geometrik kriterlere göre doğrular ve atış segmenti oluşturur.
    /// </summary>
    private static ShotSegment? ValidateAndBuildSegment(
        List<(TimeSpan Timestamp, double Value)> smoothed,
        int maxIdx,
        int minIdx,
        int shotIndex,
        ShotSegmentationOptions options)
    {
        double maxValue = smoothed[maxIdx].Value;
        double minValue = smoothed[minIdx].Value;

        // Kriter 1: Minimum açı değişimi
        double angleChange = maxValue - minValue;
        if (angleChange < options.MinAngleChangeDegrees)
            return null;

        // Kriter 2: Minimum çekiş süresi
        double drawDuration = (smoothed[minIdx].Timestamp - smoothed[maxIdx].Timestamp).TotalSeconds;
        if (drawDuration < options.MinDrawDurationSec)
            return null;

        // Kriter 3: Anchor platosu - minimum noktadan sonra açı kısa bir süre o civarda kalmalı
        int plateauEndIdx = FindPlateauEnd(smoothed, minIdx, options.AnchorPlateauToleranceDeg);
        double plateauDuration = (smoothed[plateauEndIdx].Timestamp - smoothed[minIdx].Timestamp).TotalSeconds;
        if (plateauDuration < options.AnchorPlateauMinSec)
            return null;

        // Kriter 4: Bırakma sonrası toparlanma - açı belirgin şekilde tekrar açılmalı
        int? recoveryIdx = FindRecoveryPeak(smoothed, plateauEndIdx, options.RecoveryMinAngleChangeDeg);
        if (recoveryIdx is null)
            return null;

        return new ShotSegment(
            ShotIndex: shotIndex,
            DrawStart: smoothed[maxIdx].Timestamp,
            AnchorTime: smoothed[minIdx].Timestamp,
            ReleaseTime: smoothed[plateauEndIdx].Timestamp,
            End: smoothed[recoveryIdx.Value].Timestamp);
    }

    /// <summary>
    /// Anchor platosu sonunu bulur: minimum noktadan sonra açının tolerans içinde kaldığı son nokta.
    /// </summary>
    private static int FindPlateauEnd(
        List<(TimeSpan Timestamp, double Value)> smoothed,
        int minIdx,
        double tolerance)
    {
        double minValue = smoothed[minIdx].Value;
        int plateauEndIdx = minIdx;

        for (int i = minIdx + 1; i < smoothed.Count; i++)
        {
            if (Math.Abs(smoothed[i].Value - minValue) <= tolerance)
            {
                plateauEndIdx = i;
            }
            else
            {
                break;
            }
        }

        return plateauEndIdx;
    }

    /// <summary>
    /// Bırakma sonrası toparlanma pikini bulur: açının belirgin şekilde tekrar açıldığı nokta.
    /// </summary>
    private static int? FindRecoveryPeak(
        List<(TimeSpan Timestamp, double Value)> smoothed,
        int startIdx,
        double minRecoveryAngle)
    {
        double startValue = smoothed[startIdx].Value;

        for (int i = startIdx + 1; i < smoothed.Count; i++)
        {
            if (smoothed[i].Value - startValue >= minRecoveryAngle)
            {
                return i;
            }
        }

        return null;
    }
}
