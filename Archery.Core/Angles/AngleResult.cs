namespace Archery.Core.Angles;

/// <summary>
/// Bir kare için hesaplanan açı sonucu.
/// <see cref="Degrees"/> değeri <c>null</c> ise açı hesaplanamadı demektir
/// (eksik veya güvensiz landmark).
/// </summary>
/// <param name="Definition">Hangi açı tanımı için hesaplandı.</param>
/// <param name="Degrees">Açı değeri (derece, [0, 180]) veya hesaplanamazsa <c>null</c>.</param>
/// <param name="Confidence">Hesaplama güven seviyesi.</param>
public record AngleResult(
    AngleDefinition Definition,
    double? Degrees,
    AngleConfidence Confidence);
