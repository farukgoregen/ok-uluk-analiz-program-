using Archery.Core.Signals;
using Xunit;

namespace Archery.Core.Tests.Signals;

public class ShotDetectionPipelineTests
{
    private const double Dt = 1.0 / 30.0;

    private static List<TimedSample> Series(double from, double to, Func<double, double?> f)
    {
        var list = new List<TimedSample>();
        for (double t = from; t <= to + 1e-9; t += Dt)
            list.Add(new TimedSample(TimeSpan.FromSeconds(t), f(t)));
        return list;
    }

    // Çekme kolu: 0–1 sn düz (170°), 1–2 sn çekiş, 2 sn'den itibaren holdSec boyunca 30° bekleme, ardından bırakma (170°)
    private static double? DrawArm(double t, double holdSec)
    {
        if (t < 1) return 170;
        if (t < 2) return 170 - 140 * (t - 1);
        if (t < 2 + holdSec) return 30;
        return 170;
    }

    private static List<TimedSample> Bow(double to) => Series(0, to, _ => 175);

    [Fact]
    public void LongHold_DetectedAsShot()
    {
        var draw = Series(0, 8, t => DrawArm(t, 2.5));

        var shots = ShotDetectionPipeline.Detect(draw, Bow(8));

        Assert.Single(shots);
        Assert.InRange(shots[0].ReleaseTime.TotalSeconds, 4.3, 4.6);
    }

    [Fact]
    public void ShortHold_LikeNockingAnArrow_Rejected()
    {
        var draw = Series(0, 8, t => DrawArm(t, 0.4));

        var shots = ShotDetectionPipeline.Detect(draw, Bow(8));

        Assert.Empty(shots);
    }

    [Fact]
    public void SingleFramePoseSpikeDuringHold_DoesNotEndShot()
    {
        // 3. saniyede iki karelik sıçrama (poz modeli hatası); gerçek bırakma 4.5 sn
        var draw = Series(0, 8, t => t >= 3.0 && t < 3.0 + 2 * Dt ? 140 : DrawArm(t, 2.5));

        var shots = ShotDetectionPipeline.Detect(draw, Bow(8));

        Assert.Single(shots);
        Assert.InRange(shots[0].ReleaseTime.TotalSeconds, 4.3, 4.6);
    }

    [Fact]
    public void HoldMadeOfDataGap_Rejected()
    {
        // Kısa çekişten sonra okçu kadrajdan çıkıyor: bekleme boyunca ölçüm yok, 5.5 sn'de tek bir kare
        var draw = Series(0, 8, t => t >= 2.2 && t < 5.5 ? null : DrawArm(t, 3.5));

        var shots = ShotDetectionPipeline.Detect(draw, Bow(8));

        Assert.Empty(shots);
    }

    [Fact]
    public void BowArmBent_Rejected()
    {
        var draw = Series(0, 8, t => DrawArm(t, 2.5));
        var bow = Series(0, 8, _ => 100);

        var shots = ShotDetectionPipeline.Detect(draw, bow);

        Assert.Empty(shots);
    }

    [Fact]
    public void ShotsAreRenumberedAfterRejection()
    {
        // 1. hareket kısa (ok takma), 2. hareket gerçek atış: tek atış, numarası 0
        var draw = Series(0, 14, t => t < 6 ? DrawArm(t, 0.4) : DrawArm(t - 6, 2.5));

        var shots = ShotDetectionPipeline.Detect(draw, Bow(14));

        Assert.Single(shots);
        Assert.Equal(0, shots[0].ShotIndex);
        Assert.True(shots[0].AnchorTime.TotalSeconds > 7);
    }
}
