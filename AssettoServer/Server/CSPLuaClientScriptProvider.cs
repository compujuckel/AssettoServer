using System.Collections.Generic;

namespace AssettoServer.Server;

public class CSPLuaClientScriptProvider
{
    internal List<string> Scripts { get; } = new();
    
    private readonly ACServer _server;

    public CSPLuaClientScriptProvider(ACServer server)
    {
        _server = server;
    }
    
    public void AddLuaClientScript(string script)
    {
        Scripts.Add(script);
        _server.CSPServerExtraOptions.ExtraOptions += $"\r\n[SCRIPT_...]\r\nSCRIPT='http://{_server.GeoParams.Ip}:{_server.Configuration.Server.HttpPort}/api/scripts/{Scripts.Count - 1}'\r\n";
    }
}
