using Archery.Core.Signals;

namespace Archery.Core.Tests.Signals;

public class CurveNormalizerTests
{
    [Fact]
    public void NormalizeShot_DownsamplesLongSeriesToFixedCount()
    {
        // Arrange
        // 100 noktalı bir seri, AnchorTime=0, ReleaseTime=1.0 (saniye)
        var series = new List<TimedSample>();
        for (int i = 0; i < 100; i++)
        {
            double t = i * 0.01;
            series.Add(new TimedSample(TimeSpan.FromSeconds(t), i));
        }

        var shot = new ShotSegment(0, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromSeconds(0.99), TimeSpan.FromSeconds(1.0));

        // Act
        var normalized = CurveNormalizer.NormalizeShot(series, shot, sampleCount: 50);

        // Assert
        Assert.NotNull(normalized);
        Assert.Equal(50, normalized.Samples.Count);
        // İlk değer 0 (t=0)
        Assert.Equal(0.0, normalized.Samples[0], 0.01);
        // Son değer 99 (t=0.99, shot.ReleaseTime)
        Assert.Equal(99.0, normalized.Samples[^1], 0.01);
    }

    [Fact]
    public void NormalizeShot_UpsamplesShortSeriesToFixedCount()
    {
        // Arrange
        // Sadece 10 noktalı bir seri
        var series = new List<TimedSample>();
        for (int i = 0; i < 10; i++)
        {
            double t = i * 0.1;
            // Değerler 0'dan 90'a lineer artsın
            series.Add(new TimedSample(TimeSpan.FromSeconds(t), i * 10));
        }

        var shot = new ShotSegment(0, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromSeconds(0.9), TimeSpan.FromSeconds(1.0));

        // Act
        var normalized = CurveNormalizer.NormalizeShot(series, shot, sampleCount: 50);

        // Assert
        Assert.NotNull(normalized);
        Assert.Equal(50, normalized.Samples.Count);
        Assert.Equal(0.0, normalized.Samples[0], 0.01);
        Assert.Equal(90.0, normalized.Samples[^1], 0.01);
        
        // Ortadaki bir değerin interpolasyonu doğru mu? Index 25 (0'dan 49'a kadar).
        // x = 25 / 49 = 0.510204...
        // Beklenen değer: 90 * (25 / 49) = 45.918
        Assert.Equal(45.918, normalized.Samples[25], 0.01);
    }

    [Fact]
    public void NormalizeShot_IgnoresNullValues()
    {
        // Arrange
        var series = new List<TimedSample>
        {
            new TimedSample(TimeSpan.FromSeconds(0.0), 10.0),
            new TimedSample(TimeSpan.FromSeconds(0.5), null), // Bu örnek atlanmalı
            new TimedSample(TimeSpan.FromSeconds(1.0), 20.0)
        };

        var shot = new ShotSegment(0, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.5));

        // Act
        // 3 nokta isteyelim: 0.0, 0.5 ve 1.0 zamanlarına karşılık gelmeli
        var normalized = CurveNormalizer.NormalizeShot(series, shot, sampleCount: 3);

        // Assert
        Assert.NotNull(normalized);
        Assert.Equal(3, normalized.Samples.Count);
        
        // 0.0'daki değer 10.0
        Assert.Equal(10.0, normalized.Samples[0], 0.01);
        // 1.0'daki değer 20.0
        Assert.Equal(20.0, normalized.Samples[2], 0.01);
        // 0.5'teki değer null'dı, interpolasyonla 10 ile 20'nin tam ortası (15.0) olmalı
        Assert.Equal(15.0, normalized.Samples[1], 0.01);
    }

    [Fact]
    public void NormalizeShot_ThrowsOnInvalidSampleCount()
    {
        var shot = new ShotSegment(0, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.5));
        var series = new List<TimedSample> { new TimedSample(TimeSpan.Zero, 10.0) };

        Assert.Throws<ArgumentException>(() =>
            CurveNormalizer.NormalizeShot(series, shot, sampleCount: 1));
    }

    [Fact]
    public void NormalizeShot_WindowStartsAtAnchorNotDrawStart()
    {
        // Arrange: çekiş 0 sn'de başlıyor, el yüze (anchor) 2 sn'de geliyor, bırakma 3 sn
        var series = new List<TimedSample>();
        for (int i = 0; i <= 40; i++)
        {
            double t = i * 0.1;
            series.Add(new TimedSample(TimeSpan.FromSeconds(t), t * 10));
        }

        var shot = new ShotSegment(0, TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3.5));

        // Act
        var normalized = CurveNormalizer.NormalizeShot(series, shot, sampleCount: 11);

        // Assert: eğri anchor (2 sn → 20) ile bırakma (3 sn → 30) arasını kapsar, çekiş fazı dahil değil
        Assert.NotNull(normalized);
        Assert.Equal(20.0, normalized.Samples[0], 0.01);
        Assert.Equal(30.0, normalized.Samples[^1], 0.01);
    }
}
