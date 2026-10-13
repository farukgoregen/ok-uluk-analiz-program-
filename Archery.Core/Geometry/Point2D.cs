namespace Archery.Core.Geometry;

/// <summary>
/// Görüntü düzlemindeki bir noktayı temsil eder (piksel koordinatları).
/// Bu değer 2D izdüşümdür; 3D uzaydaki gerçek konumu değil,
/// kameranın görüntü düzlemine yansıyan konumu ifade eder.
/// </summary>
public readonly record struct Point2D(double X, double Y);
