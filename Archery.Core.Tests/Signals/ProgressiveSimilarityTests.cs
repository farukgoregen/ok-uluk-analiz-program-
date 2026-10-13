using Archery.Core.Signals;

namespace Archery.Core.Tests.Signals;

public class ProgressiveSimilarityTests
{
    [Fact]
    public void ProgressiveSimilarity_LastValueEqualsFinalSimilarity()
    {
        // Arrange: başta aynı, sonra ayrışan iki eğri
        var reference = new NormalizedCurve(new double[] { 40, 40, 40, 40, 40 });
        var target = new NormalizedCurve(new double[] { 40, 40, 40, 55, 70 });

        // Act
        var progressive = ShotHistoryGenerator.ProgressiveSimilarity(reference, target);
        double final = CurveComparer.Compare(reference, target).SimilarityPercent;

        // Assert
        Assert.Equal(5, progressive.Count);
        Assert.Equal(100.0, progressive[0], 3);   // ilk noktada fark yok
        Assert.Equal(100.0, progressive[2], 3);   // ayrışma başlamadan önce
        Assert.True(progressive[4] < progressive[3]); // ayrıştıkça düşer
        Assert.Equal(final, progressive[^1], 6);
    }

    [Fact]
    public void Analyze_RowsCarryReleaseAsEndTimeAndProgressiveForNonReference()
    {
        // Arrange
        var series = new List<TimedSample>();
        for (int i = 0; i <= 100; i++)
            series.Add(new TimedSample(TimeSpan.FromSeconds(i * 0.1), 40.0 + i % 5));

        var shots = new[]
        {
            new ShotSegment(0, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3.5)),
            new ShotSegment(1, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(8.5))
        };

        // Act
        var result = new ShotHistoryGenerator().AnalyzeWithExternalSegments(series, shots);

        // Assert: tek atışlık snapshot'ta satır REF'tir, canlı benzerlik yoktur
        Assert.Null(result.Snapshots[0].Rows[0].ProgressiveSimilarity);

        var rows = result.Snapshots[^1].Rows;
        Assert.Equal(1.0, rows[0].StartTimeSec);  // eğri tam çekişte başlar
        Assert.Equal(3.0, rows[0].EndTimeSec);    // eğri bırakmada biter
        Assert.NotNull(rows[1].ProgressiveSimilarity);
        Assert.Equal(rows[1].SimilarityPercent!.Value, rows[1].ProgressiveSimilarity![^1], 6);
    }
}
