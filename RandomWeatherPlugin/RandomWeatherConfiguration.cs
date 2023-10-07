﻿using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Weather;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace RandomWeatherPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class RandomWeatherConfiguration : IValidateConfiguration<RandomWeatherConfigurationValidator>
{
    public Dictionary<WeatherFxType, float> WeatherWeights { get; init; } = new();

    public int MinWeatherDurationMinutes { get; set; } = 5;
    public int MaxWeatherDurationMinutes { get; set; } = 30;

    public int MinTransitionDurationSeconds { get; set; } = 120;
    public int MaxTransitionDurationSeconds { get; set; } = 600;

    [YamlIgnore] public int MinWeatherDurationMilliseconds => MinWeatherDurationMinutes * 60_000;
    [YamlIgnore] public int MaxWeatherDurationMilliseconds => MaxWeatherDurationMinutes * 60_000;
    [YamlIgnore] public int MinTransitionDurationMilliseconds => MinTransitionDurationSeconds * 1_000;
    [YamlIgnore] public int MaxTransitionDurationMilliseconds => MaxTransitionDurationSeconds * 1_000;
}
