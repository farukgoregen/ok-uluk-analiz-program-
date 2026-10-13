using Archery.Core.Signals;

namespace Archery.Core.Tests.Signals;

public class DataQualityTests
{
    private static ShotSegment Shot(double drawStart, double release) =>
        new(0, TimeSpan.FromSeconds(drawStart), TimeSpan.FromSeconds(drawStart), TimeSpan.FromSeconds(release), TimeSpan.FromSeconds(release));

    [Fact]
    public void MaxDataGapSeconds_FindsLongestNullRun()
    {
        // Arrange: 0.1 sn aralıklı seri, 1.0–2.0 sn arası ölçüm yok
        var series = new List<TimedSample>();
        for (int i = 0; i <= 30; i++)
        {
            double t = i * 0.1;
            double? value = t > 1.05 && t < 1.95 ? null : 100.0;
            series.Add(new TimedSample(TimeSpan.FromSeconds(t), value));
        }

        // Act
        double gap = ShotHistoryGenerator.MaxDataGapSeconds(series, Shot(0.0, 3.0));

        // Assert: son geçerli 1.0 sn, sonraki geçerli 2.0 sn
        Assert.Equal(1.0, gap, 3);
    }

    [Fact]
    public void MaxDataGapSeconds_CountsMissingDataAtWindowEdges()
    {
        // Arrange: pencere 0–2 sn, ilk ölçüm 0.8 sn'de
        var series = new List<TimedSample>();
        for (int i = 8; i <= 20; i++)
            series.Add(new TimedSample(TimeSpan.FromSeconds(i * 0.1), 100.0));

        // Act
        double gap = ShotHistoryGenerator.MaxDataGapSeconds(series, Shot(0.0, 2.0));

        // Assert
        Assert.Equal(0.8, gap, 3);
    }

    [Fact]
    public void AnalyzeWithExternalSegments_FlagsShotWithLongGap()
    {
        // Arrange: iki atış penceresi; ikincisinde 1 sn ölçüm yok
        var series = new List<TimedSample>();
        for (int i = 0; i <= 60; i++)
        {
            double t = i * 0.1;
            double? value = t > 4.05 && t < 4.95 ? null : 100.0 + i % 7;
            series.Add(new TimedSample(TimeSpan.FromSeconds(t), value));
        }
        var shots = new[] { Shot(0.0, 2.0), Shot(3.5, 5.5) };

        // Act
        var result = new ShotHistoryGenerator().AnalyzeWithExternalSegments(series, shots);
        var rows = result.Snapshots[^1].Rows;

        // Assert
        Assert.False(rows[0].HasDataGap);
        Assert.True(rows[1].HasDataGap);
        Assert.False(result.Snapshots[^1].Ort!.HasDataGap);
    }

    [Fact]
    public void SavitzkyGolay_WindowWithNull_KeepsRawValueInsteadOfAmplifying()
    {
        // Arrange: pencerenin içinde eksik örnek var; eski davranış negatif ağırlıkları
        // yeniden normalize ederek değeri büyütüyordu
        var values = new double?[] { 0, 0, 0, null, 0, 0, 10, 50, 0, 0, 0, 0 };
        var series = values
            .Select((v, i) => new TimedSample(TimeSpan.FromSeconds(i * 0.1), v))
            .ToList();

        // Act
        var filtered = SavitzkyGolayFilter.Apply(series, windowSize: 7, polynomialOrder: 2);

        // Assert: index 6'nın penceresi (3..9) null içeriyor → ham değer korunur
        Assert.Equal(10.0, filtered[6].Value);
        Assert.Null(filtered[3].Value);
    }
}
