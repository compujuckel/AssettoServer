using AssettoServer.Server.Weather;

namespace VotingWeatherPlugin;

public class VotingWeatherConfiguration
{
    public List<WeatherFxType> BlacklistedWeathers { get; init; } = new() { WeatherFxType.None };
    public int NumChoices { get; init; } = 3;
    public int VotingIntervalMinutes { get; init; } = 10;
    public int VotingDurationSeconds { get; init; } = 30;
}