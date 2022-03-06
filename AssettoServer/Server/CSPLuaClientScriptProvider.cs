using System.Collections.Generic;
using Serilog;

namespace AssettoServer.Server;

public class CSPLuaClientScriptProvider
{
    internal List<string> Scripts { get; } = new();
    
    private readonly ACServer _server;

    public CSPLuaClientScriptProvider(ACServer server)
    {
        _server = server;
    }
    
    public void AddLuaClientScript(string script, string? debugFilename = null)
    {
        bool debug = false;
        #if DEBUG
        debug = true;
        #endif
        
        if (debug && !string.IsNullOrEmpty(debugFilename))
        {
            Log.Warning("Loading Lua script {File} locally, don't forget to sync changes for release", debugFilename);
            _server.CSPServerExtraOptions.ExtraOptions += $"\r\n[SCRIPT_...]\r\nSCRIPT='{debugFilename}'\r\n";
        }
        else
        {
            Scripts.Add(script);
            _server.CSPServerExtraOptions.ExtraOptions += $"\r\n[SCRIPT_...]\r\nSCRIPT='http://{_server.GeoParams.Ip}:{_server.Configuration.Server.HttpPort}/api/scripts/{Scripts.Count - 1}'\r\n";
        }
    }
}
