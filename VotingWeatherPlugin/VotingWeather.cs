using AssettoServer.Server;
using AssettoServer.Server.Weather;
using AssettoServer.Server.Weather.Implementation;

namespace VotingWeatherPlugin;

public class VotingWeather : WeatherFxV1Implementation
{
    private VotingWeatherConfiguration _configuration;

    public VotingWeather(ACServer server, VotingWeatherConfiguration configuration) : base(server)
    {
        _configuration = configuration;
    }
}