using AssettoServer.Commands;
using Qmmands;
using Serilog;

namespace GroupStreetRacingPlugin
{
    public class GroupStreetRacingCommandModule : ACModuleBase
    {
        [Command("sampleplugin")]
        public void GroupStreetRacingPlugin()
        {
            Reply("Hello from sample plugin!");
        }
    }
}