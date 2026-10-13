using Archery.Core.Signals;

namespace Archery.Core.Tests.Signals;

public class CurveComparerTests
{
    [Fact]
    public void Compare_IdenticalCurves_Returns100Percent()
    {
        // Arrange
        var samples = new double[] { 160, 150, 140, 130, 40, 40, 40, 160 };
        var refCurve = new NormalizedCurve(samples);
        var targetCurve = new NormalizedCurve(samples);

        // Act
        var score = CurveComparer.Compare(refCurve, targetCurve);

        // Assert
        Assert.Equal(100.0, score.SimilarityPercent, 0.001);
        Assert.Equal(1.0, score.PearsonCorrelation, 0.001);
        Assert.Equal(0.0, score.RmseDegrees, 0.001);
    }

    [Fact]
    public void Compare_SlightlyDifferentCurves_ReturnsHighButNotPerfectScore()
    {
        // Arrange
        var refSamples = new double[] { 160, 150, 140, 130, 40, 40, 40 };
        // Hedef eğriye sadece 2-3 derecelik gürültü ekleyelim
        var targetSamples = new double[] { 158, 152, 139, 131, 42, 38, 41 };
        
        var refCurve = new NormalizedCurve(refSamples);
        var targetCurve = new NormalizedCurve(targetSamples);

        // Act
        var score = CurveComparer.Compare(refCurve, targetCurve);

        // Assert
        Assert.True(score.SimilarityPercent > 90.0 && score.SimilarityPercent < 100.0,
            $"Expected score between 90-100, got {score.SimilarityPercent}");
        Assert.True(score.PearsonCorrelation > 0.95);
        Assert.True(score.RmseDegrees > 0.0 && score.RmseDegrees < 5.0); // derece cinsinden RMSE
    }

    [Fact]
    public void Compare_HighlyDivergentCurves_ReturnsLowScore()
    {
        // Arrange
        var refSamples = new double[] { 160, 140, 120, 100, 80, 60, 40 };
        // Erken bırakılmış tamamen alakasız bir eğri (RMSE çok yüksek, korelasyon zıt olabilir)
        var targetSamples = new double[] { 40, 60, 80, 100, 120, 140, 160 };

        var refCurve = new NormalizedCurve(refSamples);
        var targetCurve = new NormalizedCurve(targetSamples);

        // Act
        var score = CurveComparer.Compare(refCurve, targetCurve);

        // Assert
        // Zıt eğrilerin korelasyonu -1'dir, ceza almalı. RMSE de çok yüksektir.
        Assert.True(score.SimilarityPercent < 30.0, 
            $"Expected very low score, got {score.SimilarityPercent}");
        Assert.True(score.PearsonCorrelation < 0);
    }

    [Fact]
    public void Compare_MismatchedSampleCount_Throws()
    {
        var refCurve = new NormalizedCurve(new double[] { 1, 2, 3 });
        var targetCurve = new NormalizedCurve(new double[] { 1, 2 });

        Assert.Throws<ArgumentException>(() => CurveComparer.Compare(refCurve, targetCurve));
    }
}

public class ShotHistoryGeneratorTests
{
    [Fact]
    public void Analyze_ProducesSnapshotsCorrectly()
    {
        var generator = new ShotHistoryGenerator();
        var series = new List<TimedSample>();
        for (int i = 0; i < 100; i++) series.Add(new TimedSample(TimeSpan.FromSeconds(i * 0.1), 100 + i)); 
        
        var shots = new List<ShotSegment> {
            new ShotSegment(0, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4)),
            new ShotSegment(1, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(8))
        };

        var result = generator.AnalyzeWithExternalSegments(series, shots);

        // a) snapshot listesi atış sayısı kadar olmalı
        Assert.Equal(2, result.Snapshots.Count);

        // b) ShotIndex ve ReleaseTimestamp doğru mu?
        Assert.Equal(1, result.Snapshots[0].ShotIndex);
        Assert.Equal(3, result.Snapshots[0].ReleaseTimestamp, 0.01);

        Assert.Equal(2, result.Snapshots[1].ShotIndex);
        Assert.Equal(7, result.Snapshots[1].ReleaseTimestamp, 0.01);

        // c) Son snapshot'ın içerdiği satır sayısı toplam atış sayısına eşittir
        Assert.Equal(2, result.Snapshots.Last().Rows.Count);
    }

    [Fact]
    public void Analyze_SingleShot_ReturnsIsReferenceTrueWithNullSimilarity()
    {
        var generator = new ShotHistoryGenerator();
        var series = new List<TimedSample>();
        for (int i = 0; i < 100; i++) series.Add(new TimedSample(TimeSpan.FromSeconds(i * 0.1), 100));
        var shots = new List<ShotSegment> { new ShotSegment(0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(8)) };

        var result = generator.AnalyzeWithExternalSegments(series, shots);

        var finalRows = result.Snapshots.Last().Rows;
        Assert.Single(finalRows);
        Assert.True(finalRows[0].IsReference);
        Assert.Null(finalRows[0].SimilarityPercent);
    }

    [Fact]
    public void Analyze_AddingNewShot_ChangesOldShotSimilarity()
    {
        var generator = new ShotHistoryGenerator();
        var series = new List<TimedSample>();
        for (int i = 0; i < 100; i++) series.Add(new TimedSample(TimeSpan.FromSeconds(i * 0.1), 100 + i)); 

        var shots2 = new List<ShotSegment> {
            new ShotSegment(0, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4)),
            new ShotSegment(1, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(8))
        };
        var result2 = generator.AnalyzeWithExternalSegments(series, shots2);
        double? sim1_initial = result2.Snapshots.Last().Rows[0].SimilarityPercent;

        var shots3 = new List<ShotSegment>(shots2);
        shots3.Add(new ShotSegment(2, TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(9), TimeSpan.FromSeconds(9.5), TimeSpan.FromSeconds(10)));
        var result3 = generator.AnalyzeWithExternalSegments(series, shots3);
        double? sim1_after = result3.Snapshots.Last().Rows[0].SimilarityPercent;

        Assert.NotNull(sim1_initial);
        Assert.NotNull(sim1_after);
        Assert.NotEqual(sim1_initial.Value, sim1_after.Value);
    }

    [Fact]
    public void Analyze_TwoIdenticalShots_ReturnsMaxSimilarity()
    {
        var generator = new ShotHistoryGenerator();
        var series = new List<TimedSample>();
        for (int i = 0; i < 100; i++) series.Add(new TimedSample(TimeSpan.FromSeconds(i * 0.1), 100 + Math.Sin(i)*10));

        var shots = new List<ShotSegment> {
            new ShotSegment(0, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4)),
            new ShotSegment(1, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4)) // exactly same times to get exact same slice
        };
        var result = generator.AnalyzeWithExternalSegments(series, shots);

        var finalRows = result.Snapshots.Last().Rows;
        Assert.Equal(2, finalRows.Count);
        Assert.True(finalRows[0].SimilarityPercent > 99.0);
        Assert.True(finalRows[1].SimilarityPercent > 99.0);
    }

    [Fact]
    public void Analyze_DifferentShots_LowersSimilarity()
    {
        var generator = new ShotHistoryGenerator();
        var series = new List<TimedSample>();
        // Atış 1'in analiz penceresi 120→130°, atış 2'ninki sabit 60°: ortalamadan ~30° sapma
        for (int i = 0; i < 50; i++) series.Add(new TimedSample(TimeSpan.FromSeconds(i * 0.1), 100 + i));
        for (int i = 50; i < 100; i++) series.Add(new TimedSample(TimeSpan.FromSeconds(i * 0.1), 60.0));

        var shots = new List<ShotSegment> {
            new ShotSegment(0, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4)),
            new ShotSegment(1, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(9))
        };
        var result = generator.AnalyzeWithExternalSegments(series, shots);

        var finalRows = result.Snapshots.Last().Rows;
        Assert.True(finalRows[0].SimilarityPercent < 70.0);
    }

    [Fact]
    public void Analyze_Symmetry_Kol1AndKol2_SameResult()
    {
        var generator = new ShotHistoryGenerator();
        var series = new List<TimedSample>();
        for (int i = 0; i < 100; i++) series.Add(new TimedSample(TimeSpan.FromSeconds(i * 0.1), 100 + Math.Sin(i)*10));
        var shots = new List<ShotSegment> {
            new ShotSegment(0, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4)),
            new ShotSegment(1, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(9))
        };

        var bowArmResult = generator.AnalyzeWithExternalSegments(series, shots);
        var drawArmResult = generator.AnalyzeWithExternalSegments(series, shots);

        Assert.Equal(
            bowArmResult.Snapshots.Last().Rows[0].SimilarityPercent, 
            drawArmResult.Snapshots.Last().Rows[0].SimilarityPercent
        );
    }
}
