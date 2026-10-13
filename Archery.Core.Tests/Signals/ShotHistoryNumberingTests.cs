using Archery.Core.Signals;
using Xunit;

namespace Archery.Core.Tests.Signals;

public class ShotHistoryNumberingTests
{
    [Fact]
    public void ShotWithoutDataInThisArm_DoesNotShiftNumbering()
    {
        // Yay kolu 1. atışta kadraj dışında (ölçüm yok), 2. atışta görünüyor
        var series = new List<TimedSample>();
        for (double t = 0; t <= 10; t += 1.0 / 30.0)
            series.Add(new TimedSample(TimeSpan.FromSeconds(t), t < 5 ? null : 170));

        var shots = new List<ShotSegment>
        {
            new(0, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3.5)),
            new(1, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(8.5))
        };

        var history = new ShotHistoryGenerator().AnalyzeWithExternalSegments(series, shots);

        var row = Assert.Single(history.Snapshots.Last().Rows);
        Assert.Equal(2, row.ShotNumber);
    }
}
