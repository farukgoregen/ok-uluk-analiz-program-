using Archery.Core.Geometry;

namespace Archery.Core.Tests.Geometry;

public class AngleCalculatorTests
{
    private const double Tolerance = 0.0001;

    [Fact]
    public void RightAngle_Returns90Degrees()
    {
        // 90° — dik açı
        var u = new Vector2D(1, 0);
        var v = new Vector2D(0, 1);

        double? result = AngleCalculator.ComputeAngleDegrees(u, v);

        Assert.NotNull(result);
        Assert.Equal(90.0, result.Value, Tolerance);
    }

    [Fact]
    public void StraightAngle_Returns180Degrees()
    {
        // 180° — düz açı (acos'un sorunlu olduğu bölge)
        var u = new Vector2D(1, 0);
        var v = new Vector2D(-1, 0);

        double? result = AngleCalculator.ComputeAngleDegrees(u, v);

        Assert.NotNull(result);
        Assert.Equal(180.0, result.Value, Tolerance);
    }

    [Fact]
    public void ParallelVectors_Returns0Degrees()
    {
        // 0° — paralel (aynı yönlü) vektörler
        var u = new Vector2D(3, 4);
        var v = new Vector2D(6, 8);

        double? result = AngleCalculator.ComputeAngleDegrees(u, v);

        Assert.NotNull(result);
        Assert.Equal(0.0, result.Value, Tolerance);
    }

    [Fact]
    public void FortyFiveDegreeAngle_Returns45Degrees()
    {
        // 45° açı
        var u = new Vector2D(1, 0);
        var v = new Vector2D(1, 1);

        double? result = AngleCalculator.ComputeAngleDegrees(u, v);

        Assert.NotNull(result);
        Assert.Equal(45.0, result.Value, Tolerance);
    }

    [Fact]
    public void ZeroVector_ReturnsNull()
    {
        // Sıfır büyüklüklü vektör → açı tanımsız
        var u = new Vector2D(1, 0);
        var zero = new Vector2D(0, 0);

        Assert.Null(AngleCalculator.ComputeAngleDegrees(u, zero));
        Assert.Null(AngleCalculator.ComputeAngleDegrees(zero, u));
    }

    [Fact]
    public void NearStraightAngle_IsStable()
    {
        // 179.99° civarı — acos ile sayısal sorun oluşabilecek bölge, atan2 ile kararlı olmalı
        double almostOppositeX = -1.0;
        double almostOppositeY = 0.0001;
        var u = new Vector2D(1, 0);
        var v = new Vector2D(almostOppositeX, almostOppositeY);

        double? result = AngleCalculator.ComputeAngleDegrees(u, v);

        Assert.NotNull(result);
        Assert.True(result.Value > 179.0 && result.Value <= 180.0,
            $"179-180° aralığında olmalı, ama {result.Value}° geldi.");
    }
}
