namespace Archery.Core.Signals;

/// <summary>
/// Bir atışın zaman sınırlarını temsil eder.
/// Tüm zamanlar video zaman damgasıdır (PTS).
/// </summary>
/// <param name="ShotIndex">Atış sıra numarası (0-tabanlı).</param>
/// <param name="DrawStart">Çekiş başlangıcı — açının azalmaya başladığı an.</param>
/// <param name="AnchorTime">Tam çekiş (anchor) anı — minimum açı noktası.</param>
/// <param name="ReleaseTime">Bırakma anı — açının ani sıçradığı an.</param>
/// <param name="End">Atış sonu — açının tekrar platoya ulaştığı veya bir sonraki çekişin başladığı an.</param>
public record ShotSegment(
    int ShotIndex,
    TimeSpan DrawStart,
    TimeSpan AnchorTime,
    TimeSpan ReleaseTime,
    TimeSpan End);
