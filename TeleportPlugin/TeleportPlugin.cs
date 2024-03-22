using System.Reflection;
using AssettoServer.Server;

namespace TeleportPlugin;

public class TeleportPlugin
{
    public TeleportPlugin(CSPServerScriptProvider scriptProvider)
    {
        scriptProvider.AddScript(
            new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("TeleportPlugin.lua.teleport-to-location.lua")!).ReadToEnd(),
            "teleport-to-location.lua"
        );
        scriptProvider.AddScript(
            new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("TeleportPlugin.lua.teleport-to-car.lua")!).ReadToEnd(),
            "teleport-to-car.lua"
        );
    }
}
