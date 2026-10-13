namespace Archery.Web.Models;

public record ShotHistoryDto(
    List<PanelSnapshotDto> BowArmSnapshots,
    List<PanelSnapshotDto> DrawArmSnapshots
);

public record PanelSnapshotDto(
    int ShotIndex,
    double ReleaseTimestamp,
    List<ShotRecordDto> Rows,
    ShotRecordDto? Ort
);

public record ShotRecordDto(
    int ShotNumber,
    bool IsReference,
    double? SimilarityPercent,
    IReadOnlyList<double> CurveSamples,
    double? StartTimeSec,
    double? EndTimeSec,
    bool HasDataGap,
    double MaxDataGapSec,
    IReadOnlyList<double>? ProgressiveSimilarity,
    double? DurationSec
);
