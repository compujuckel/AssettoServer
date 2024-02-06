using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Weather;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace DynamicWeatherPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class DynamicWeatherConfiguration : IValidateConfiguration<DynamicWeatherConfigurationValidator>
{
    [YamlMember(Description = "Weights for weather transition, only listed weathers will be counted, setting a weight to 0 blacklists a weather")]
    public Dictionary<WeatherFxType, Dictionary<WeatherFxType, float>> WeatherTransitions { get; init; } = new();

    [YamlMember(Description = "Minimum duration until next weather change")]
    public int MinWeatherDurationMinutes { get; set; } = 5;
    [YamlMember(Description = "Maximum duration until next weather change")]
    public int MaxWeatherDurationMinutes { get; set; } = 30;

    [YamlMember(Description = "Minimum weather transition duration")]
    public int MinTransitionDurationSeconds { get; set; } = 120;
    [YamlMember(Description = "Maximum weather transition duration")]
    public int MaxTransitionDurationSeconds { get; set; } = 600;

    [YamlIgnore] public int MinWeatherDurationMilliseconds => MinWeatherDurationMinutes * 60_000;
    [YamlIgnore] public int MaxWeatherDurationMilliseconds => MaxWeatherDurationMinutes * 60_000;
    [YamlIgnore] public int MinTransitionDurationMilliseconds => MinTransitionDurationSeconds * 1_000;
    [YamlIgnore] public int MaxTransitionDurationMilliseconds => MaxTransitionDurationSeconds * 1_000;
}
