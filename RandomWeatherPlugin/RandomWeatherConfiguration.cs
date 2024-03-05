using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Weather;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace RandomWeatherPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class RandomWeatherConfiguration : IValidateConfiguration<RandomWeatherConfigurationValidator>
{
    [YamlMember(Description = "Which mode should be used for weather randomization \nAvailable values: 'Default' and 'TransitionTable'")]
    public RandomWeatherMode Mode { get; set; } = RandomWeatherMode.Default;
    
    [YamlMember(Description = "Minimum duration until next weather change")]
    public int MinWeatherDurationMinutes { get; set; } = 5;
    [YamlMember(Description = "Maximum duration until next weather change")]
    public int MaxWeatherDurationMinutes { get; set; } = 30;

    [YamlMember(Description = "Minimum weather transition duration")]
    public int MinTransitionDurationSeconds { get; set; } = 120;
    [YamlMember(Description = "Maximum weather transition duration")]
    public int MaxTransitionDurationSeconds { get; set; } = 600;
    
    [YamlMember(Description = "Weights for weather transition, only listed weathers will be counted\nCheck the reference config to see the structure")]
    public Dictionary<WeatherFxType, Dictionary<WeatherFxType, float>> WeatherTransitions { get; init; } = new();
    [YamlMember(Description = "Weights for random weather selection, removing a weight or setting it to 0 blacklists a weather\nYou can also use decimals like 0.1")]
    public Dictionary<WeatherFxType, float> WeatherWeights { get; init; } = new()
    {
        { WeatherFxType.LightThunderstorm, 1.0f },
        { WeatherFxType.Thunderstorm, 1.0f },
        { WeatherFxType.HeavyThunderstorm, 1.0f },
        { WeatherFxType.LightDrizzle, 1.0f },
        { WeatherFxType.Drizzle, 1.0f },
        { WeatherFxType.HeavyDrizzle, 1.0f },
        { WeatherFxType.LightRain, 1.0f },
        { WeatherFxType.Rain, 1.0f },
        { WeatherFxType.HeavyRain, 1.0f },
        { WeatherFxType.LightSnow, 1.0f },
        { WeatherFxType.Snow, 1.0f },
        { WeatherFxType.HeavySnow, 1.0f },
        { WeatherFxType.LightSleet, 1.0f },
        { WeatherFxType.Sleet, 1.0f },
        { WeatherFxType.HeavySleet, 1.0f },
        { WeatherFxType.Clear, 1.0f },
        { WeatherFxType.FewClouds, 1.0f },
        { WeatherFxType.ScatteredClouds, 1.0f },
        { WeatherFxType.BrokenClouds, 1.0f },
        { WeatherFxType.OvercastClouds, 1.0f },
        { WeatherFxType.Fog, 1.0f },
        { WeatherFxType.Mist, 1.0f },
        { WeatherFxType.Smoke, 1.0f },
        { WeatherFxType.Haze, 1.0f },
        { WeatherFxType.Sand, 1.0f },
        { WeatherFxType.Dust, 1.0f },
        { WeatherFxType.Squalls, 1.0f },
        { WeatherFxType.Tornado, 1.0f },
        { WeatherFxType.Hurricane, 1.0f },
        { WeatherFxType.Cold, 1.0f },
        { WeatherFxType.Hot, 1.0f },
        { WeatherFxType.Windy, 1.0f },
        { WeatherFxType.Hail, 1.0f },
    };

    [YamlIgnore] public int MinWeatherDurationMilliseconds => MinWeatherDurationMinutes * 60_000;
    [YamlIgnore] public int MaxWeatherDurationMilliseconds => MaxWeatherDurationMinutes * 60_000;
    [YamlIgnore] public int MinTransitionDurationMilliseconds => MinTransitionDurationSeconds * 1_000;
    [YamlIgnore] public int MaxTransitionDurationMilliseconds => MaxTransitionDurationSeconds * 1_000;
}

public enum RandomWeatherMode
{
    Default = 0,
    TransitionTable = 1
}
