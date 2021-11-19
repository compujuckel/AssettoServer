using AssettoServer.Server.Weather;

namespace VotingWeatherPlugin;

public class VotingWeatherConfiguration
{
    public List<WeatherFxType> BlacklistedWeathers { get; init; } = new() { WeatherFxType.None };
    public int VotingIntervalMinutes { get; init; } = 10;
}