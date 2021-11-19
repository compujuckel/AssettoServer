using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using Serilog;

namespace SamplePlugin;

public class SamplePlugin : IAssettoServerPlugin
{
    public void Initialize(ACServer server)
    {
        Log.Debug("Sample plugin initialized");
    }
}