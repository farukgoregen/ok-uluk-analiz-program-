namespace Archery.Core.Signals;

/// <summary>
/// İki normalize eğri arasındaki benzerlik skorunu (tutarlılık) hesaplar.
/// Doğruluk değil, referansa yakınlık ölçülmektedir.
///
/// Karşılaştırma derece cinsinden yapılır: eğrilerin aynı zaman noktalarındaki açı farklarının
/// karekök ortalaması (RMSE) alınır. Böylece hem şekil hem de mutlak açı farkı hesaba katılır;
/// neredeyse sabit eğrilerde (ör. yay kolu, tam çekişteki bekleme) küçük gürültü büyütülmez.
///
/// Benzerlik = max(0, 1 - RMSE / tolerans) × 100. RMSE 0° ise %100, tolerans ve üzerindeyse %0.
/// Tolerans bir ölçek parametresidir; "ideal açı" değildir.
/// </summary>
public static class CurveComparer
{
    /// <summary>
    /// Benzerliğin %0'a düştüğü ortalama açı farkı (derece). Varsayılan: 30.
    /// </summary>
    public const double DefaultToleranceDegrees = 30.0;

    /// <summary>
    /// İki eğriyi derece cinsinden karşılaştırır.
    /// </summary>
    /// <param name="reference">Referans eğri.</param>
    /// <param name="target">Karşılaştırılan eğri.</param>
    /// <param name="toleranceDegrees">Benzerliğin %0'a düştüğü RMSE değeri (derece).</param>
    public static ConsistencyScore Compare(
        NormalizedCurve reference,
        NormalizedCurve target,
        double toleranceDegrees = DefaultToleranceDegrees)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(target);

        if (toleranceDegrees <= 0)
            throw new ArgumentException("Tolerance must be positive.", nameof(toleranceDegrees));

        if (reference.Samples.Count != target.Samples.Count)
            throw new ArgumentException("Curves must have the same number of samples.");

        if (reference.Samples.Count == 0)
            return new ConsistencyScore(0, 0, 0);

        int count = reference.Samples.Count;

        double sse = 0;
        double sumR = 0, sumT = 0, sumR2 = 0, sumT2 = 0, sumRT = 0;

        for (int i = 0; i < count; i++)
        {
            double r = reference.Samples[i];
            double t = target.Samples[i];

            double diff = r - t;
            sse += diff * diff;

            sumR += r;
            sumT += t;
            sumR2 += r * r;
            sumT2 += t * t;
            sumRT += r * t;
        }

        // --- 1. Derece cinsinden RMSE → benzerlik yüzdesi ---
        double rmseDegrees = Math.Sqrt(sse / count);
        double similarityPercent = Math.Max(0, 1.0 - rmseDegrees / toleranceDegrees) * 100.0;

        // --- 2. Pearson korelasyonu (bilgi amaçlı: eğrilerin trend benzerliği) ---
        // Eğrilerden biri sabitse korelasyon tanımsızdır; 0 döndürülür.
        double numerator = count * sumRT - sumR * sumT;
        double denominator = Math.Sqrt((count * sumR2 - sumR * sumR) * (count * sumT2 - sumT * sumT));

        double correlation = 0;
        if (denominator > 1e-10)
        {
            correlation = numerator / denominator;
            if (double.IsNaN(correlation) || double.IsInfinity(correlation))
                correlation = 0;
        }

        return new ConsistencyScore(similarityPercent, correlation, rmseDegrees);
    }
}
