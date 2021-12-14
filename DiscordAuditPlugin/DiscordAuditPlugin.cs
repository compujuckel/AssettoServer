using AssettoServer.Server;
using AssettoServer.Server.Plugin;

namespace DiscordAuditPlugin;

public class DiscordAuditPlugin : IAssettoServerPlugin<DiscordConfiguration>
{
    private DiscordConfiguration _configuration;

    public void Initialize(ACServer server)
    {
        _ = new Discord(server, _configuration);
    }

    public void SetConfiguration(DiscordConfiguration configuration)
    {
        _configuration = configuration;
    }
}