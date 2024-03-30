namespace ReplayPlugin;

public class ReplayConfiguration
{
    public int MinSegmentSizeKilobytes { get; set; } = 250;
    public int MaxSegmentSizeKilobytes { get; set; } = 10_000;
    public int SegmentTargetSeconds { get; set; } = 30;
    public int ReplayDurationSeconds { get; set; } = 60;

    public int MinSegmentSizeBytes => MinSegmentSizeKilobytes * 1000;
    public int MaxSegmentSizeBytes => MaxSegmentSizeKilobytes * 1000;
    public int SegmentTargetMilliseconds => SegmentTargetSeconds * 1000;
    public int ReplayDurationMilliseconds => ReplayDurationSeconds * 1000;
}
