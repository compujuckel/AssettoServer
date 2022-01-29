using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;

namespace WordFilterPlugin;

public class WordFilterPlugin : IAssettoServerPlugin<WordFilterConfiguration>
{
    private WordFilter? _instance;
    private WordFilterConfiguration? _configuration;

    public void SetConfiguration(WordFilterConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Initialize(ACServer server)
    {
        if (_configuration == null)
            throw new ConfigurationException("No configuration found for WordFilterPlugin");
        
        _instance = new WordFilter(server, _configuration);
    }
}