namespace Archery.Core.Signals;

/// <summary>
/// Birden fazla normalize eğrinin noktasal (point-wise) ortalamasını alır.
/// </summary>
public static class CurveAverager
{
    /// <summary>
    /// Verilen eğri listesinin ortalamasını hesaplar.
    /// Liste boşsa null döner. Tüm eğrilerin boyutu aynı olmalıdır.
    /// </summary>
    public static NormalizedCurve? Average(IReadOnlyList<NormalizedCurve> curves)
    {
        if (curves == null || curves.Count == 0)
            return null;

        int sampleCount = curves[0].Samples.Count;

        // Tüm eğrilerin boyutlarının tutarlı olduğunu doğrula
        for (int i = 1; i < curves.Count; i++)
        {
            if (curves[i].Samples.Count != sampleCount)
                throw new ArgumentException($"All curves must have the same number of samples. Curve at index {i} differs.");
        }

        var averages = new double[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            double sum = 0;
            foreach (var curve in curves)
            {
                sum += curve.Samples[i];
            }
            averages[i] = sum / curves.Count;
        }

        return new NormalizedCurve(averages);
    }
}
