using Archery.Core.Signals;

namespace Archery.Core.Tests.Signals;

public class CurveAveragerTests
{
    [Fact]
    public void Average_NullOrEmptyList_ReturnsNull()
    {
        Assert.Null(CurveAverager.Average(null!));
        Assert.Null(CurveAverager.Average(Array.Empty<NormalizedCurve>()));
    }

    [Fact]
    public void Average_SingleCurve_ReturnsSameCurve()
    {
        var curve = new NormalizedCurve(new double[] { 10, 20, 30 });
        var avg = CurveAverager.Average(new[] { curve });

        Assert.NotNull(avg);
        Assert.Equal(10, avg.Samples[0]);
        Assert.Equal(20, avg.Samples[1]);
        Assert.Equal(30, avg.Samples[2]);
    }

    [Fact]
    public void Average_MultipleCurves_CalculatesPointwiseAverage()
    {
        var curve1 = new NormalizedCurve(new double[] { 10, 20, 30 });
        var curve2 = new NormalizedCurve(new double[] { 30, 40, 50 });
        var curve3 = new NormalizedCurve(new double[] { 20, 60, 100 });

        var avg = CurveAverager.Average(new[] { curve1, curve2, curve3 });

        Assert.NotNull(avg);
        // İndeks 0: (10 + 30 + 20) / 3 = 20
        Assert.Equal(20.0, avg.Samples[0], 0.001);
        // İndeks 1: (20 + 40 + 60) / 3 = 40
        Assert.Equal(40.0, avg.Samples[1], 0.001);
        // İndeks 2: (30 + 50 + 100) / 3 = 60
        Assert.Equal(60.0, avg.Samples[2], 0.001);
    }

    [Fact]
    public void Average_MismatchedLength_ThrowsArgumentException()
    {
        var curve1 = new NormalizedCurve(new double[] { 1, 2, 3 });
        var curve2 = new NormalizedCurve(new double[] { 1, 2 });

        Assert.Throws<ArgumentException>(() => CurveAverager.Average(new[] { curve1, curve2 }));
    }
}
