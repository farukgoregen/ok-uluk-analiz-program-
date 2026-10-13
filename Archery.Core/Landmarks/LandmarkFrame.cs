namespace Archery.Core.Landmarks;

/// <summary>
/// Bir video karesine ait tüm landmark noktaları.
/// <see cref="Timestamp"/> video zaman damgasıdır (PTS).
/// Kare numarası veya sabit FPS varsayımı yapılmaz (telefon videoları değişken kare hızlı olabilir).
/// </summary>
/// <param name="Timestamp">Video zaman damgası (PTS).</param>
/// <param name="Points">Landmark adı → nokta eşlemesi.</param>
public record LandmarkFrame(
    TimeSpan Timestamp,
    IReadOnlyDictionary<string, LandmarkPoint> Points);
