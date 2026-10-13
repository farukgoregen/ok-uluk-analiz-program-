namespace Archery.Core.Signals;

/// <summary>
/// Savitzky-Golay yumuşatma filtresi (sıfır faz / gecikmesiz).
/// Hareketli ortalamaya göre daha iyi pik genişliğini ve yüksek frekanslı
/// sinyal özelliklerini korur. Zaman ekseninde ileri-geri uygulanır.
/// </summary>
public static class SavitzkyGolayFilter
{
    /// <summary>
    /// Verilen seriye Savitzky-Golay filtresi uygular.
    /// </summary>
    /// <param name="samples">Zaman damgalı örnekler.</param>
    /// <param name="windowSize">Pencere boyutu (tek sayı, en az 3).</param>
    /// <param name="polynomialOrder">Polinom derecesi (1 veya 2).</param>
    /// <returns>Filtrelenmiş örnekler (aynı zaman damgaları, null değerler korunur).</returns>
    public static IReadOnlyList<TimedSample> Apply(
        IReadOnlyList<TimedSample> samples,
        int windowSize = 7,
        int polynomialOrder = 2)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
            return Array.Empty<TimedSample>();

        // Pencere boyutunu ve polinom derecesini sınırla
        windowSize = Math.Max(3, windowSize | 1); // tek sayı yap
        polynomialOrder = Math.Clamp(polynomialOrder, 1, 2);
        windowSize = Math.Max(windowSize, polynomialOrder + 2);

        var result = new List<TimedSample>(samples.Count);
        int halfWindow = windowSize / 2;

        // Katsayıları sadece bir kere hesapla (döngü içinde hesaplamak verimsizdi ve kenarlarda hata veriyordu)
        double[] coefficients = ComputeCoefficients(windowSize, polynomialOrder);

        for (int i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];

            // Null değerleri koru
            if (!sample.Value.HasValue)
            {
                result.Add(sample);
                continue;
            }

            // Pencere sınırlarını belirle
            int start = i - halfWindow;
            int end = i + halfWindow;

            // Pencere taşıyorsa (kenarlardaysa) ham değeri kullan
            if (start < 0 || end >= samples.Count)
            {
                result.Add(sample);
                continue;
            }

            // Pencerede eksik (null) örnek varsa ham değeri kullan.
            // SG katsayıları negatif ağırlıklar içerir; eksik örnekleri atlayıp yeniden normalize etmek
            // değeri aşırı büyütür (ör. açı 0-180 dışına taşar).
            bool windowComplete = true;
            for (int j = start; j <= end; j++)
            {
                if (!samples[j].Value.HasValue)
                {
                    windowComplete = false;
                    break;
                }
            }

            if (!windowComplete)
            {
                result.Add(sample);
                continue;
            }

            // Filtre uygula (katsayıların toplamı 1'dir)
            double smoothedValue = 0;
            for (int j = start; j <= end; j++)
            {
                smoothedValue += coefficients[j - start] * samples[j].Value!.Value;
            }

            result.Add(new TimedSample(sample.Timestamp, smoothedValue));
        }

        return result;
    }

    /// <summary>
    /// Verilen pencere boyutu ve polinom derecesi için Savitzky-Golay katsayılarını hesaplar.
    /// </summary>
    private static double[] ComputeCoefficients(int windowSize, int polynomialOrder)
    {
        int halfWindow = windowSize / 2;
        var coefficients = new double[windowSize];

        // En küçük kareler çözümü ile katsayıları hesapla
        // Basitleştirilmiş yaklaşım: polinom regresyonu

        // Normal denklemler için matris oluştur
        int n = polynomialOrder + 1;
        var A = new double[n, n];
        var b = new double[n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                double sum = 0;
                for (int k = -halfWindow; k <= halfWindow; k++)
                {
                    sum += Math.Pow(k, i + j);
                }
                A[i, j] = sum;
            }
        }

        // İlk satır: sadece 0. katsayıyı (yumuşatılmış değer) hesapla
        b[0] = 1;

        // Lineer sistemi çöz (Gauss eliminasyonu)
        var solution = SolveLinearSystem(A, b);

        // Katsayıları hesapla
        for (int k = -halfWindow; k <= halfWindow; k++)
        {
            double coeff = 0;
            for (int i = 0; i < n; i++)
            {
                coeff += solution[i] * Math.Pow(k, i);
            }
            coefficients[k + halfWindow] = coeff;
        }

        return coefficients;
    }

    /// <summary>
    /// Gauss eliminasyonu ile lineer denklem sistemi çözümü.
    /// </summary>
    private static double[] SolveLinearSystem(double[,] A, double[] b)
    {
        int n = b.Length;
        var augmented = new double[n, n + 1];

        // Genişletilmiş matris oluştur
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                augmented[i, j] = A[i, j];
            }
            augmented[i, n] = b[i];
        }

        // Gauss eliminasyonu
        for (int col = 0; col < n; col++)
        {
            // Pivot bul
            int maxRow = col;
            double maxVal = Math.Abs(augmented[col, col]);
            for (int row = col + 1; row < n; row++)
            {
                if (Math.Abs(augmented[row, col]) > maxVal)
                {
                    maxVal = Math.Abs(augmented[row, col]);
                    maxRow = row;
                }
            }

            // Satırları değiştir
            if (maxRow != col)
            {
                for (int j = col; j <= n; j++)
                {
                    (augmented[col, j], augmented[maxRow, j]) = (augmented[maxRow, j], augmented[col, j]);
                }
            }

            // Sıfır pivot kontrolü
            if (Math.Abs(augmented[col, col]) < 1e-10)
                continue;

            // Eliminasyon
            for (int row = col + 1; row < n; row++)
            {
                double factor = augmented[row, col] / augmented[col, col];
                for (int j = col; j <= n; j++)
                {
                    augmented[row, j] -= factor * augmented[col, j];
                }
            }
        }

        // Geri yerine koyma
        var solution = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = augmented[i, n];
            for (int j = i + 1; j < n; j++)
            {
                sum -= augmented[i, j] * solution[j];
            }
            solution[i] = Math.Abs(augmented[i, i]) < 1e-10 ? 0 : sum / augmented[i, i];
        }

        return solution;
    }
}
