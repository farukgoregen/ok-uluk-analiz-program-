namespace Archery.Core.Geometry;

/// <summary>
/// Görüntü düzleminde 2D vektör.
/// İki nokta arasındaki yön ve mesafeyi ifade eder.
/// </summary>
public readonly record struct Vector2D(double X, double Y)
{
    /// <summary>Vektörün büyüklüğü (uzunluğu).</summary>
    public double Magnitude => Math.Sqrt(X * X + Y * Y);

    /// <summary>
    /// <paramref name="from"/> noktasından <paramref name="to"/> noktasına olan vektörü oluşturur.
    /// </summary>
    public static Vector2D FromPoints(Point2D from, Point2D to)
        => new(to.X - from.X, to.Y - from.Y);
}
