using System;
using System.Collections.Generic;
using System.Text;
using AssettoServer.Server.Configuration;
using IniParser.Model;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AssettoServer.Server;

public class CSPServerScriptProvider
{
    internal List<Func<IActionResult>> Scripts { get; } = new();

    private readonly CSPServerExtraOptions _cspServerExtraOptions;

    public CSPServerScriptProvider(CSPServerExtraOptions cspServerExtraOptions)
    {
        _cspServerExtraOptions = cspServerExtraOptions;
    }

    public virtual void AddScriptFile(string path, string? debugFilename = null, Dictionary<string, object>? configuration = null)
        => AddScriptInternal(() => new PhysicalFileResult(path, "text/x-lua") { FileDownloadName = debugFilename }, debugFilename, configuration);
    
    public virtual void AddScript(string script, string? debugFilename = null, Dictionary<string, object>? configuration = null)
    {
        var bytes = Encoding.UTF8.GetBytes(script);
        AddScriptInternal(() => new FileContentResult(bytes, "text/x-lua") { FileDownloadName = debugFilename }, debugFilename, configuration);
    }

    private void AddScriptInternal(Func<IActionResult> script, string? debugFilename = null, Dictionary<string, object>? configuration = null)
    {
        var data = new IniData();
        var scriptSection = data[$"SCRIPT_{Scripts.Count}-{debugFilename}"];

        if (ACServer.IsDebugBuild && !string.IsNullOrEmpty(debugFilename))
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
