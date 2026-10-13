namespace Archery.Core.Angles;

/// <summary>
/// Okçuluk analizinde kullanılan bilinen açı tanımları.
/// Landmark adları genel (BowShoulder, DrawElbow vb.); sol/sağ elli eşlemesi
/// ayrı bir katmanda çözümlenir.
/// </summary>
public static class WellKnownAngles
{
    /// <summary>Yay kolu dirsek açısı (BowShoulder → BowElbow → BowWrist).</summary>
    public static AngleDefinition BowArmElbow { get; } = new(
        Name: "BowArmElbow",
        ProximalLandmark: "BowShoulder",
        MidLandmark: "BowElbow",
        DistalLandmark: "BowWrist");

    /// <summary>Çekiş kolu dirsek açısı (DrawShoulder → DrawElbow → DrawWrist).</summary>
    public static AngleDefinition DrawArmElbow { get; } = new(
        Name: "DrawArmElbow",
        ProximalLandmark: "DrawShoulder",
        MidLandmark: "DrawElbow",
        DistalLandmark: "DrawWrist");

    /// <summary>Tüm tanımlı açıların listesi.</summary>
    public static IReadOnlyList<AngleDefinition> All { get; } = new[]
    {
        BowArmElbow,
        DrawArmElbow
    };
}
