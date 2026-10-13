namespace Archery.Core.Angles;

/// <summary>
/// Hesaplanan açının güven seviyesi.
/// </summary>
public enum AngleConfidence
{
    /// <summary>Tüm landmark'lar yeterli güvenle algılandı.</summary>
    High,

    /// <summary>En az bir landmark düşük güvenle algılandı.</summary>
    Low,

    /// <summary>En az bir landmark eksik veya güven eşiğinin çok altında; açı üretilmedi.</summary>
    Insufficient
}
