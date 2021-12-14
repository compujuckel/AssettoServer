using AssettoServer.Server.Weather;
using YamlDotNet.Serialization;

namespace VotingWeatherPlugin;

public class VotingWeatherConfiguration
{
    public List<WeatherFxType> BlacklistedWeathers { get; init; } = new() { WeatherFxType.None };
    public int NumChoices { get; init; } = 3;
    public int VotingIntervalMinutes { get; init; } = 10;
    public int VotingDurationSeconds { get; init; } = 30;

    [YamlIgnore] public int VotingIntervalMilliseconds => VotingIntervalMinutes * 60_000;
    [YamlIgnore] public int VotingDurationMilliseconds => VotingDurationSeconds * 1000;
}