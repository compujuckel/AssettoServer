using System.Collections.Generic;
using IniParser.Model;
using Serilog;

namespace AssettoServer.Server;

public class CSPServerScriptProvider
{
    internal List<string> Scripts { get; } = new();
    
    private readonly ACServer _server;

    public CSPServerScriptProvider(ACServer server)
    {
        _server = server;
    }
    
    public void AddScript(string script, string? debugFilename = null, Dictionary<string, object>? configuration = null)
    {
        bool debug = false;
        #if DEBUG
        debug = true;
        #endif

        var data = new IniData();
        var scriptSection = data["SCRIPT_..."];

        if (debug && !string.IsNullOrEmpty(debugFilename))
        {
            Log.Warning("Loading Lua script {File} locally, don't forget to sync changes for release", debugFilename);
            scriptSection["SCRIPT"] = debugFilename;
        }
        else
        {
            Scripts.Add(script);
            scriptSection["SCRIPT"] = $"'http://{{ServerIP}}:{{ServerHTTPPort}}/api/scripts/{Scripts.Count - 1}'";
        }

        if (configuration != null)
        {
            foreach ((string key, object value) in configuration)
            {
                scriptSection.AddKey(key, value.ToString());
            }
        }

        _server.CSPServerExtraOptions.ExtraOptions += $"\r\n{data}\r\n";
    }
}
