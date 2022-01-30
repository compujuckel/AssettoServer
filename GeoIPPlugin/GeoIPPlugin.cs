using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;

namespace GeoIPPlugin;

public class GeoIPPlugin : IAssettoServerPlugin<GeoIPConfiguration>
{
    internal static GeoIP? Instance;

    private GeoIPConfiguration? _configuration; 
    
    public void SetConfiguration(GeoIPConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Initialize(ACServer server)
    {
        if (_configuration == null)
            throw new ConfigurationException("No configuration found for GeoIPPlugin");

        Instance = new GeoIP(server, _configuration);
    }
}