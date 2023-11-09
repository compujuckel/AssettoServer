using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

    public virtual void AddScriptFromResource(string resourceName, string? debugFilename = null, Dictionary<string, object>? configuration = null)
    {
        using var stream = Assembly.GetCallingAssembly().GetManifestResourceStream(resourceName)!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();
        AddScript(bytes, debugFilename, configuration);
    }

    public virtual void AddScriptFile(string path, string? debugFilename = null, Dictionary<string, object>? configuration = null)
        => AddScriptInternal(() => new PhysicalFileResult(path, "text/x-lua") { FileDownloadName = debugFilename }, debugFilename, configuration);
    
    public virtual void AddScript(string script, string? debugFilename = null, Dictionary<string, object>? configuration = null)
    {
        var bytes = Encoding.UTF8.GetBytes(script);
        AddScript(bytes, debugFilename, configuration);
    }

    public virtual void AddScript(byte[] script, string? debugFilename = null, Dictionary<string, object>? configuration = null) 
        => AddScriptInternal(() => new FileContentResult(script, "text/x-lua") { FileDownloadName = debugFilename }, debugFilename, configuration);

    private void AddScriptInternal(Func<IActionResult> script, string? debugFilename = null, Dictionary<string, object>? configuration = null)
    {
        var data = new IniData();
        var scriptSection = data[$"SCRIPT_{10 + Scripts.Count}-{debugFilename}"];

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
