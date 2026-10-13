using Archery.Core.Signals;
using Xunit;

namespace Archery.Core.Tests.Signals;

public class ShotSegmenterTests
{
    [Fact]
    public void DetectShots_NoisySmallFluctuations_NoShotDetected()
    {
        // Gürültülü ama küçük dalgalanmalı seri (genlik eşiğinin altında)
        var samples = new List<TimedSample>();
        double time = 0;
        double baseAngle = 160;

        // 20 saniye boyunca ±10° dalgalanma
        for (int i = 0; i < 200; i++)
        {
            double noise = Math.Sin(i * 0.5) * 10;
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), baseAngle + noise));
            time += 0.1;
        }

        var shots = ShotSegmenter.DetectShots(samples, new ShotSegmentationOptions());

        Assert.Empty(shots);
    }

    [Fact]
    public void DetectShots_RealShotPattern_DetectsSingleShot()
    {
        // Gerçek atış örüntüsü: 160° → 30° (çekiş), platoda kalma, sonra toparlanma
        var samples = new List<TimedSample>();
        double time = 0;

        // 0-5 sn: Kol açık (~160°)
        for (int i = 0; i < 50; i++)
        {
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), 160 + Math.Sin(i * 0.3) * 2));
            time += 0.1;
        }

        // 5-7 sn: Çekiş (160° → 30°)
        for (int i = 0; i < 20; i++)
        {
            double t = i / 20.0;
            double angle = 160 - t * 130;
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), angle));
            time += 0.1;
        }

        // 7-8 sn: Anchor platosu (~30°)
        for (int i = 0; i < 10; i++)
        {
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), 30 + Math.Sin(i) * 2));
            time += 0.1;
        }

        // 8-10 sn: Bırakma ve toparlanma (30° → 170°)
        for (int i = 0; i < 20; i++)
        {
            double t = i / 20.0;
            double angle = 30 + t * 140;
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), angle));
            time += 0.1;
        }

        var shots = ShotSegmenter.DetectShots(samples, new ShotSegmentationOptions());

        Assert.Single(shots);
        Assert.True(shots[0].DrawStart.TotalSeconds >= 4.5);
        Assert.True(shots[0].DrawStart.TotalSeconds <= 5.1);
    }

    [Fact]
    public void DetectShots_BowLiftFalsePositive_NoShotDetected()
    {
        // 5.73 sn'deki sahte pozitif senaryosu:
        // Kişi yayı kaldırıyor, kol ~160°, sonra küçük bir dalgalanma var
        var samples = new List<TimedSample>();
        double time = 0;

        // 0-5 sn: Kol açık (~160°)
        for (int i = 0; i < 50; i++)
        {
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), 160 + Math.Sin(i * 0.2) * 3));
            time += 0.1;
        }

        // 5-6 sn: Küçük dalgalanma (160° → 130° → 160°) — bu atış DEĞİL
        for (int i = 0; i < 10; i++)
        {
            double t = i / 10.0;
            double angle = 160 - Math.Sin(t * Math.PI) * 30;
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), angle));
            time += 0.1;
        }

        // 6-10 sn: Tekrar açık (~160°)
        for (int i = 0; i < 40; i++)
        {
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), 160 + Math.Sin(i * 0.3) * 2));
            time += 0.1;
        }

        var shots = ShotSegmenter.DetectShots(samples, new ShotSegmentationOptions());

        Assert.Empty(shots);
    }

    [Fact]
    public void DetectShots_TwoCloseShots_RefractoryPeriodApplies()
    {
        var samples = new List<TimedSample>();
        double time = 0;

        // Atış öncesi bekleme (ilk maksimumun bulunabilmesi için)
        for (int i = 0; i < 20; i++)
        {
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), 160 + Math.Sin(i * 0.2) * 2));
            time += 0.1;
        }

        // İlk atış
        for (int i = 0; i < 30; i++)
        {
            double t = i / 15.0;
            double angle = i < 15 ? 160 - t * 130 : 30 + Math.Sin(i) * 2;
            if (i >= 20) angle = 30 + ((i - 20) / 10.0) * 140; // Recovery
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), angle));
            time += 0.1;
        }

        // İkinci atış (refrakter süre içinde)
        for (int i = 0; i < 30; i++)
        {
            double t = i / 15.0;
            double angle = i < 15 ? 160 - t * 130 : 30 + Math.Sin(i) * 2;
            if (i >= 20) angle = 30 + ((i - 20) / 10.0) * 140; // Recovery
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), angle));
            time += 0.1;
        }

        var options = new ShotSegmentationOptions { RefractoryPeriodSec = 5.0 };
        var shots = ShotSegmenter.DetectShots(samples, options);

        Assert.Single(shots);
    }

    [Fact]
    public void DetectShots_TwoSeparateShots_DetectsBoth()
    {
        var samples = new List<TimedSample>();
        double time = 0;

        // Atış öncesi bekleme
        for (int i = 0; i < 20; i++)
        {
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), 160 + Math.Sin(i * 0.2) * 2));
            time += 0.1;
        }

        // İlk atış
        for (int i = 0; i < 30; i++)
        {
            double t = i / 15.0;
            double angle = i < 15 ? 160 - t * 130 : 30 + Math.Sin(i) * 2;
            if (i >= 20) angle = 30 + ((i - 20) / 10.0) * 140;
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), angle));
            time += 0.1;
        }

        // Ara (5 sn bekleme)
        for (int i = 0; i < 50; i++)
        {
            double angle = 160 + Math.Sin(i * 0.2) * 2;
            if (i == 49) angle = 165; // Kesin tepe
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), angle));
            time += 0.1;
        }

        // İkinci atış
        for (int i = 0; i < 30; i++)
        {
            double t = i / 15.0;
            double angle = i < 15 ? 160 - t * 130 : 30 + Math.Sin(i) * 2;
            if (i >= 20) angle = 30 + ((i - 20) / 10.0) * 140;
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), angle));
            time += 0.1;
        }

        var shots = ShotSegmenter.DetectShots(samples, new ShotSegmentationOptions());

        Assert.Equal(2, shots.Count);
    }

    [Fact]
    public void DetectShots_LowConfidenceFrames_ExcludedFromDetection()
    {
        // Düşük güvenli karelerin atış tespiti hesabına katılmadığını doğrula
        var samples = new List<TimedSample>();
        double time = 0;

        // 0-5 sn: Geçerli veri
        for (int i = 0; i < 50; i++)
        {
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), 160));
            time += 0.1;
        }

        // 5-7 sn: Null değerler (düşük güven)
        for (int i = 0; i < 20; i++)
        {
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), null));
            time += 0.1;
        }

        // 7-10 sn: Geçerli veri
        for (int i = 0; i < 30; i++)
        {
            samples.Add(new TimedSample(TimeSpan.FromSeconds(time), 30));
            time += 0.1;
        }

        var shots = ShotSegmenter.DetectShots(samples, new ShotSegmentationOptions());

        // Null değerler atlandığı için atış tespit edilmemeli
        Assert.Empty(shots);
    }
}
