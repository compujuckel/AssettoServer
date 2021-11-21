using AssettoServer.Server;
using AssettoServer.Server.Plugin;

namespace WordFilterPlugin;

public class WordFilterPlugin : IAssettoServerPlugin<WordFilterConfiguration>
{
    private WordFilter _instance;
    private WordFilterConfiguration _configuration;

    public void SetConfiguration(WordFilterConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Initialize(ACServer server)
    {
        _instance = new WordFilter(server, _configuration);
    }
}