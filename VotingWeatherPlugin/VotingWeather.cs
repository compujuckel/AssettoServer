using AssettoServer.Server;

namespace VotingWeatherPlugin;

public class VotingWeather
{
    private ACServer _server;
    private VotingWeatherConfiguration _configuration;

    public VotingWeather(ACServer server, VotingWeatherConfiguration configuration)
    {
        _server = server;
        _configuration = configuration;
    }
}