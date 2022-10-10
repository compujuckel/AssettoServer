using AssettoServer.Commands;
using Qmmands;
using Serilog;

namespace RandomDynamicTrafficPlugin
{
    public class RandomDynamicTrafficCommandModule : ACModuleBase
    {
        [Command("randomdynamictrafficplugin")]
        public void RandomDynamicTrafficPlugin()
        {
            Reply("Hello from RandomDynamicTraffic plugin!");
        }
    }
}
