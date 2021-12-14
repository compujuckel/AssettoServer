using AssettoServer.Commands;
using Qmmands;
using Serilog;

namespace SamplePlugin;

public class SampleCommandModule : ACModuleBase
{
    [Command("sampleplugin")]
    public void SamplePlugin()
    {
        Reply("Hello from sample plugin!");
    }
}