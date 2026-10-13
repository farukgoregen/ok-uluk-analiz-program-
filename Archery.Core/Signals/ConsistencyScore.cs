namespace Archery.Core.Signals;

/// <summary>
/// İki normalize eğri arasındaki benzerlik/tutarlılık analizinin sonucu.
/// </summary>
/// <param name="SimilarityPercent">Genel benzerlik yüzdesi [0-100]. Doğruluk değil, referansa yakınlıktır.</param>
/// <param name="PearsonCorrelation">Eğrilerin trend benzerliği [-1, 1] (bilgi amaçlı; yüzdeye katılmaz).</param>
/// <param name="RmseDegrees">Eğriler arası açı farklarının karekök ortalaması (derece).</param>
public record ConsistencyScore(
    double SimilarityPercent,
    double PearsonCorrelation,
    double RmseDegrees);
