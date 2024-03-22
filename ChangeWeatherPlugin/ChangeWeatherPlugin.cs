using System.Reflection;
using AssettoServer.Server;

namespace ChangeWeatherPlugin;

public class ChangeWeatherPlugin
{
    public ChangeWeatherPlugin(CSPServerScriptProvider scriptProvider)
    {
        scriptProvider.AddScript(
            new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("ChangeWeatherPlugin.lua.change-weather.lua")!).ReadToEnd(),
            "change-weather.lua"
        );
    }
}
