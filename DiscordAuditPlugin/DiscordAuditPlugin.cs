using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;

namespace DiscordAuditPlugin;

public class DiscordAuditPlugin : IAssettoServerPlugin<DiscordConfiguration>
{
    private DiscordConfiguration? _configuration;

    public void Initialize(ACServer server)
    {
        if (_configuration == null)
            throw new ConfigurationException("No configuration found for DiscordAuditPlugin");
        
        _ = new Discord(server, _configuration);
    }

    public void SetConfiguration(DiscordConfiguration configuration)
    {
        _configuration = configuration;
    }
}