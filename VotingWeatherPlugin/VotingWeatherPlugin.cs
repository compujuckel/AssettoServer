using AssettoServer.Server;
using AssettoServer.Server.Plugin;

namespace VotingWeatherPlugin;

public class VotingWeatherPlugin : IAssettoServerPlugin<VotingWeatherConfiguration>
{
    internal static VotingWeather Instance { get; private set; }
    
    private VotingWeatherConfiguration _configuration;
    
    public void Initialize(ACServer server)
    {
        Instance = new VotingWeather(server, _configuration);
        _ = Instance.LoopAsync();
    }

    public void SetConfiguration(VotingWeatherConfiguration configuration)
    {
        _configuration = configuration;
    }
}