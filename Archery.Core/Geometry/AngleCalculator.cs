namespace Archery.Core.Geometry;

/// <summary>
/// İki vektör arasındaki açıyı hesaplar.
/// Formül: atan2(|u×v|, u·v). acos KULLANILMAZ (175–180° civarında sayısal kararsızlık).
/// </summary>
public static class AngleCalculator
{
    /// <summary>
    /// <paramref name="u"/> ve <paramref name="v"/> vektörleri arasındaki açıyı derece cinsinden döndürür.
    /// Vektörlerden biri sıfır büyüklüklüyse <c>null</c> döner (açı tanımsız).
    /// </summary>
    /// <param name="u">Birinci vektör.</param>
    /// <param name="v">İkinci vektör.</param>
    /// <returns>Açı (derece, [0, 180]) veya tanımsızsa <c>null</c>.</returns>
    public static double? ComputeAngleDegrees(Vector2D u, Vector2D v)
    {
        // Sıfır büyüklüklü vektörde açı tanımsızdır.
        const double epsilon = 1e-12;
        if (u.Magnitude < epsilon || v.Magnitude < epsilon)
            return null;

        // 2D çapraz çarpım skaler bileşeni: u.X * v.Y - u.Y * v.X
        double cross = Math.Abs(u.X * v.Y - u.Y * v.X);

        // Nokta çarpım: u.X * v.X + u.Y * v.Y
        double dot = u.X * v.X + u.Y * v.Y;

        double radians = Math.Atan2(cross, dot);
        return radians * (180.0 / Math.PI);
    }
}
