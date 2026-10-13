using Archery.Core.Geometry;
using Archery.Core.Landmarks;

namespace Archery.Core.Angles;

/// <summary>
/// Bir <see cref="LandmarkFrame"/> üzerinde tanımlı açıları hesaplar.
/// </summary>
public static class FrameAngleComputer
{
    /// <summary>
    /// Verilen karedeki landmark noktalarını kullanarak her açı tanımı için sonuç üretir.
    /// </summary>
    /// <param name="frame">Landmark noktalarını içeren kare.</param>
    /// <param name="definitions">Hesaplanacak açı tanımları.</param>
    /// <param name="minConfidence">
    /// Bir landmark'ın geçerli sayılması için gereken minimum güven değeri [0, 1].
    /// Varsayılan: 0.5.
    /// </param>
    /// <returns>Her tanım için bir <see cref="AngleResult"/>.</returns>
    public static IReadOnlyList<AngleResult> ComputeAngles(
        LandmarkFrame frame,
        IReadOnlyList<AngleDefinition> definitions,
        double minConfidence = 0.5)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(definitions);

        var results = new AngleResult[definitions.Count];

        for (int i = 0; i < definitions.Count; i++)
        {
            results[i] = ComputeSingle(frame, definitions[i], minConfidence);
        }

        return results;
    }

    private static AngleResult ComputeSingle(
        LandmarkFrame frame,
        AngleDefinition definition,
        double minConfidence)
    {
        // Landmark'ları bul
        bool hasProximal = frame.Points.TryGetValue(definition.ProximalLandmark, out var proximal);
        bool hasMid = frame.Points.TryGetValue(definition.MidLandmark, out var mid);
        bool hasDistal = frame.Points.TryGetValue(definition.DistalLandmark, out var distal);

        // Eksik landmark → açı üretilmez
        if (!hasProximal || !hasMid || !hasDistal)
        {
            return new AngleResult(definition, Degrees: null, AngleConfidence.Insufficient);
        }

        // Güven kontrolü: herhangi biri eşiğin altındaysa açı üretilmez
        double minDetected = Math.Min(proximal!.Confidence, Math.Min(mid!.Confidence, distal!.Confidence));

        if (minDetected < minConfidence)
        {
            return new AngleResult(definition, Degrees: null, AngleConfidence.Low);
        }

        // Vektörler: mid noktasından proximal'a ve distal'a
        var u = Vector2D.FromPoints(mid.Position, proximal.Position);
        var v = Vector2D.FromPoints(mid.Position, distal.Position);

        double? degrees = AngleCalculator.ComputeAngleDegrees(u, v);

        // Sıfır büyüklüklü vektör (üst üste binen noktalar) → açı tanımsız
        if (degrees is null)
        {
            return new AngleResult(definition, Degrees: null, AngleConfidence.Insufficient);
        }

        return new AngleResult(definition, degrees, AngleConfidence.High);
    }
}
