using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using JetBrains.Annotations;

namespace ReportPlugin;

[UsedImplicitly]
public class ReportPluginHolder : IAssettoServerPlugin<ReportConfiguration>
{
    internal static ReportPlugin Instance = null!;

    private ReportConfiguration? _configuration;
    
    public void SetConfiguration(ReportConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Initialize(ACServer server)
    {
        if (_configuration == null)
        {
            throw new ConfigurationException("No configuration found for ReportPlugin");
        }
        
        Instance = new ReportPlugin(server, _configuration);
    }
}
