using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using AssettoServer.Server.Weather;

namespace VotingWeatherPlugin;

public class VotingWeatherPlugin : IAssettoServerPlugin<VotingWeatherConfiguration>, IWeatherPlugin
{
    public VotingWeather Instance { get; private set; }
    
    private VotingWeatherConfiguration _configuration;
    
    public void Initialize(ACServer server)
    {
        Instance = new VotingWeather(server, _configuration);
    }

    public void SetConfiguration(VotingWeatherConfiguration configuration)
    {
        _configuration = configuration;
    }
}