using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace RandomTrackPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class RandomTrackConfiguration : IValidateConfiguration<RandomWeatherConfigurationValidator>
{
    public List<RandomTrack> TrackWeights { get; init; } = new();

    public int TrackDurationMinutes { get; set; } = 5;

    [YamlIgnore] public int TrackDurationMilliseconds => MinWeatherDurationMinutes * 60_000;
}

public class RandomTrack
{
    public required string DisplayName { get; init; }
    public required string TrackFolder { get; init; }
    public required string TrackLayoutConfig { get; init; }
    public required float Weight { get; init; }
}
