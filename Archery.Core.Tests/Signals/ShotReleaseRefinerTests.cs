using Archery.Core.Signals;
using Xunit;

namespace Archery.Core.Tests.Signals;

public class ShotReleaseRefinerTests
{
    private static List<TimedSample> Series(double from, double to, Func<double, double> f)
    {
        var list = new List<TimedSample>();
        for (double t = from; t <= to + 1e-9; t += 0.033)
            list.Add(new TimedSample(TimeSpan.FromSeconds(t), f(t)));
        return list;
    }

    // Çekme kolu 35° plato, 56.5 sn'de 5°'ye çöküyor; segmentasyon bırakmayı 57.4'te bulmuş
    private static double DrawArm(double t) => t < 56.5 ? 35 : 5;

    private static readonly ShotSegment Shot = new(0,
        TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(54), TimeSpan.FromSeconds(57.4), TimeSpan.FromSeconds(57.6));

    [Fact]
    public void BowArmCollapse_MovesReleaseToStartOfDrawArmCollapse()
    {
        var draw = Series(53, 58, DrawArm);
        var bow = Series(53, 58, t => t < 57.2 ? 175 : 110); // bırakmadan hemen önce yay indiriliyor

        var refined = ShotReleaseRefiner.Refine(Shot, draw, bow, new ShotSegmentationOptions());

        Assert.InRange(refined.ReleaseTime.TotalSeconds, 56.3, 56.7);
        Assert.Equal(Shot.AnchorTime, refined.AnchorTime);
        Assert.Equal(Shot.End, refined.End);
    }

    [Fact]
    public void BowArmUpright_LeavesShotUntouched()
    {
        var draw = Series(53, 58, DrawArm);
        var bow = Series(53, 58, _ => 175);

        var refined = ShotReleaseRefiner.Refine(Shot, draw, bow, new ShotSegmentationOptions());

        Assert.Equal(Shot, refined);
    }

    [Fact]
    public void BowArmCollapse_WithoutDrawArmCollapse_LeavesShotUntouched()
    {
        var draw = Series(53, 58, _ => 35);
        var bow = Series(53, 58, t => t < 57.2 ? 175 : 110);

        var refined = ShotReleaseRefiner.Refine(Shot, draw, bow, new ShotSegmentationOptions());

        Assert.Equal(Shot, refined);
    }
}
