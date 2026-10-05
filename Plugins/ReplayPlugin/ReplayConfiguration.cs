using AssettoServer.Server.Configuration;
using YamlDotNet.Serialization;

namespace ReplayPlugin;

public class ReplayConfiguration : IValidateConfiguration<ReplayConfigurationValidator>
{
    public int MinSegmentSizeKilobytes { get; set; } = 250;
    public int MaxSegmentSizeKilobytes { get; set; } = 10_000;
    public int SegmentTargetSeconds { get; set; } = 30;
    public int ReplayDurationSeconds { get; set; } = 60;
    public int RefreshRateDivisor { get; set; } = 1;

    [YamlIgnore] public int MinSegmentSizeBytes => MinSegmentSizeKilobytes * 1000;
    [YamlIgnore] public int MaxSegmentSizeBytes => MaxSegmentSizeKilobytes * 1000;
    [YamlIgnore] public int SegmentTargetMilliseconds => SegmentTargetSeconds * 1000;
    [YamlIgnore] public int ReplayDurationMilliseconds => ReplayDurationSeconds * 1000;
}
