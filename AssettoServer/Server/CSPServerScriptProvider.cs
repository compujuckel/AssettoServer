using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.UserGroup;
using IniParser.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AssettoServer.Server;

[PublicAPI]
public class CSPServerScriptProvider
{
    internal List<Func<IActionResult>> Scripts { get; } = new();

    private readonly CSPServerExtraOptions _cspServerExtraOptions;
    private readonly IUserGroup? _debugUserGroup;

    private string _serverScripts = "";
    private string _serverScriptsDebug = "";

    public CSPServerScriptProvider(CSPServerExtraOptions cspServerExtraOptions,
        ACServerConfiguration configuration,
        UserGroupManager userGroupManager,
        CSPServerExtraOptions extraOptions)
    {
        _cspServerExtraOptions = cspServerExtraOptions;

        if (!string.IsNullOrEmpty(configuration.Extra.DebugScriptUserGroup))
        {
            _debugUserGroup = userGroupManager.Resolve(configuration.Extra.DebugScriptUserGroup);
        }
        
        extraOptions.CSPServerExtraOptionsSending += OnExtraOptionsSending;
    }

    private async void OnExtraOptionsSending(ACTcpClient sender, CSPServerExtraOptionsSendingEventArgs args)
    {
        using var _ = args.GetDeferral();
        if (_debugUserGroup != null && await _debugUserGroup.ContainsAsync(sender.Guid))
        {
            args.Builder.Append(_serverScriptsDebug);
        }
        else
        {
            args.Builder.Append(_serverScripts);
        }
    }

    public virtual void AddScript(Stream stream, string? debugFilename = null, Dictionary<string, object>? configuration = null, bool leaveOpen = false)
    {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();
        AddScript(bytes, debugFilename, configuration);

        if (!leaveOpen)
        {
            stream.Dispose();
        }
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

    private string PrepareScriptSection(string address, string? debugFilename = null, Dictionary<string, object>? configuration = null)
    {
        var data = new IniData();
        var scriptSection = data[$"SCRIPT_{10 + Scripts.Count}-{debugFilename}"];
        scriptSection["SCRIPT"] = address;
        
        if (configuration != null)
        {
            foreach ((string key, object value) in configuration)
            {
                scriptSection.AddKey(key, value.ToString());
            }
        }

        return data.ToString();
    }

    private void AddScriptInternal(Func<IActionResult> script, string? debugFilename = null, Dictionary<string, object>? configuration = null)
    {
        string section;
        if (Program.IsDebugBuild && !string.IsNullOrEmpty(debugFilename))
        {
            Log.Warning("Loading Lua script {File} locally, don't forget to sync changes for release", debugFilename);
            section = PrepareScriptSection(debugFilename, debugFilename, configuration);
        }
        else
        {
            section = PrepareScriptSection($"'http://{{ServerIP}}:{{ServerHTTPPort}}/api/scripts/{Scripts.Count}'", debugFilename, configuration);
        }
        
        _serverScripts += $"\r\n{section}\r\n";

        if (!string.IsNullOrEmpty(debugFilename))
        {
            Dictionary<string, object>? debugConfig = null;
            if (configuration != null)
            {
                debugConfig = new Dictionary<string, object>(configuration);
                debugConfig.Remove("CHECKSUM");
            }

            section = PrepareScriptSection(debugFilename, debugFilename, debugConfig);
            _serverScriptsDebug += $"\r\n{section}\r\n";
        }

        Scripts.Add(script);
    }
}
