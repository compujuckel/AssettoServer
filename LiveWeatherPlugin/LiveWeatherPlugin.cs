using AssettoServer.Server;
using AssettoServer.Server.Plugin;

namespace LiveWeatherPlugin;

public class LiveWeatherPlugin : IAssettoServerPlugin<LiveWeatherConfiguration>
{
    internal static LiveWeatherProvider Instance;
    
    private LiveWeatherConfiguration _configuration;
    
    public void SetConfiguration(LiveWeatherConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Initialize(ACServer server)
    {
        Instance = new LiveWeatherProvider(server, _configuration);
        _ = Instance.LoopAsync();
    }
}