using AssettoServer.Server;
using AssettoServer.Server.Plugin;

namespace ChangeWeatherPlugin;

public class ChangeWeatherPlugin : IAssettoServerPlugin
{
    internal static ChangeWeather? Instance { get; private set; }

    public void Initialize(ACServer server)
    {
        Instance = new ChangeWeather(server);
    }
}
