using Archery.Core.Geometry;

namespace Archery.Core.Landmarks;

/// <summary>
/// Tek bir landmark (eklem/nokta) noktası.
/// </summary>
/// <param name="Position">Görüntü düzlemindeki 2D konum (piksel).</param>
/// <param name="Confidence">Poz tahmini modelinin bu noktaya verdiği güven [0, 1].</param>
public record LandmarkPoint(Point2D Position, double Confidence);
