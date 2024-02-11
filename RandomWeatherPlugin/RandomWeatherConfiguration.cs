using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Weather;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace RandomWeatherPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class RandomWeatherConfiguration : IValidateConfiguration<RandomWeatherConfigurationValidator>
{
    [YamlMember(Description = "Which mode should be used for weather randomization \nAvailable values: 'Default' and 'TransitionTable'")]
    public RandomWeatherMode Mode = RandomWeatherMode.Default;
    [YamlMember(Description = "Weights for random weather selection, setting a weight to 0 blacklists a weather, default weight is 1")]
    public Dictionary<WeatherFxType, float> WeatherWeights { get; init; } = new();
    [YamlMember(Description = "Weights for weather transition, only listed weathers will be counted")]
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

public enum RandomWeatherMode
{
    Default,
    TransitionTable
}
