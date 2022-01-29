using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;

namespace LiveWeatherPlugin;

public class LiveWeatherPlugin : IAssettoServerPlugin<LiveWeatherConfiguration>
{
    internal static LiveWeatherProvider? Instance;
    
    private LiveWeatherConfiguration? _configuration;
    
    public void SetConfiguration(LiveWeatherConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Initialize(ACServer server)
    {
        if (_configuration == null)
            throw new ConfigurationException("No configuration found for LiveWeatherPlugin");
        
        Instance = new LiveWeatherProvider(server, _configuration);
        _ = Instance.LoopAsync();
    }
}