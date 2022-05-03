using System.Collections.Generic;
using AssettoServer.Server.Configuration;
using IniParser.Model;
using Serilog;

namespace AssettoServer.Server;

public class CSPServerScriptProvider
{
    internal List<string> Scripts { get; } = new();

    private readonly CSPServerExtraOptions _cspServerExtraOptions;

    public CSPServerScriptProvider(CSPServerExtraOptions cspServerExtraOptions)
    {
        _cspServerExtraOptions = cspServerExtraOptions;
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

        _cspServerExtraOptions.ExtraOptions += $"\r\n{data}\r\n";
    }
}
