namespace Archery.Core.Signals;

/// <summary>
/// Atışların zaman eksenini normalize ederek (0.0 - 1.0 aralığına) 
/// sabit boyutlu bir vektöre (örneğin 50 nokta) dönüştürür.
/// </summary>
public static class CurveNormalizer
{
    public const int DefaultSampleCount = 50;

    /// <summary>
    /// Bir atışın tam çekiş (anchor) fazındaki açıları alır ve zamanı normalize edilmiş sabit uzunlukta
    /// bir eğriye dönüştürür. Tam çekiş anını (AnchorTime — çekme elinin yüze geldiği an) 0.0,
    /// bırakma anını (ReleaseTime) 1.0 olarak kabul eder.
    /// </summary>
    /// <param name="fullSeries">Videonun tamamına ait zaman damgalı açı serisi.</param>
    /// <param name="shot">Zaman normalizasyonu yapılacak atış segmenti.</param>
    /// <param name="sampleCount">Üretilecek eğrinin nokta sayısı. Varsayılan: 50.</param>
    /// <returns>Normalize edilmiş eğri veya geçerli veri yoksa null.</returns>
    public static NormalizedCurve? NormalizeShot(
        IReadOnlyList<TimedSample> fullSeries,
        ShotSegment shot,
        int sampleCount = DefaultSampleCount)
    {
        ArgumentNullException.ThrowIfNull(fullSeries);
        ArgumentNullException.ThrowIfNull(shot);

        if (sampleCount < 2)
            throw new ArgumentException("Sample count must be at least 2", nameof(sampleCount));

        // Atış segmentindeki geçerli noktaları al (AnchorTime ile ReleaseTime arası)
        // Tam çekişten bırakmaya kadarki nişan alma fazı karşılaştırılır; çekiş ve bırakma sonrası atılır
        var segmentSamples = new List<TimedSample>();
        foreach (var sample in fullSeries)
        {
            if (sample.Timestamp >= shot.AnchorTime && sample.Timestamp <= shot.ReleaseTime)
            {
                if (sample.Value.HasValue)
                {
                    segmentSamples.Add(sample);
                }
            }
        }

        if (segmentSamples.Count < 2)
        {
            // İnterpolasyon için yeterli veri yok
            return null;
        }

        double startTime = shot.AnchorTime.TotalSeconds;
        double duration = (shot.ReleaseTime - shot.AnchorTime).TotalSeconds;

        if (duration <= 0)
        {
            return null;
        }

        var normalizedSamples = new double[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            // x: 0.0'dan 1.0'a ilerleme (yüzde)
            double x = (double)i / (sampleCount - 1);
            
            // Gerçek zamandaki karşılığı
            double targetTime = startTime + (x * duration);

            normalizedSamples[i] = Interpolate(segmentSamples, targetTime);
        }

        return new NormalizedCurve(normalizedSamples);
    }

    /// <summary>
    /// Verilen zaman noktasındaki açıyı seriden lineer interpolasyonla bulur.
    /// </summary>
    private static double Interpolate(List<TimedSample> samples, double targetTime)
    {
        // Hedef zaman serinin öncesindeyse ilk değeri, sonrasındaysa son değeri dön (clamp)
        if (targetTime <= samples[0].Timestamp.TotalSeconds)
            return samples[0].Value!.Value;

        if (targetTime >= samples[^1].Timestamp.TotalSeconds)
            return samples[^1].Value!.Value;

        // Zaman noktasını çevreleyen iki ölçümü bul
        for (int i = 0; i < samples.Count - 1; i++)
        {
            double t1 = samples[i].Timestamp.TotalSeconds;
            double t2 = samples[i + 1].Timestamp.TotalSeconds;

            if (targetTime >= t1 && targetTime <= t2)
            {
                // Lineer interpolasyon
                double v1 = samples[i].Value!.Value;
                double v2 = samples[i + 1].Value!.Value;

                // Aynı zaman damgalı ardışık örnekler: 0/0 bölmesini önle
                if (t2 <= t1)
                    return v2;

                double progress = (targetTime - t1) / (t2 - t1);
                return v1 + (v2 - v1) * progress;
            }
        }

        // Buraya asla düşmemeli (yukarıdaki clamp mantığı sayesinde)
        return samples[^1].Value!.Value;
    }
}
