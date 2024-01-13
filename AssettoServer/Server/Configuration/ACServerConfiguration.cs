using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AssettoServer.Server.Configuration.Extra;
using AssettoServer.Server.Configuration.Kunos;
using AssettoServer.Shared.Model;
using AssettoServer.Shared.Network.Http.Responses;
using AssettoServer.Utils;
using FluentValidation;
using Newtonsoft.Json;
using Serilog;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Configuration;

public class ACServerConfiguration
{
    public ServerConfiguration Server { get; }
    public EntryList EntryList { get; }
    public List<SessionConfiguration> Sessions { get; }
    [YamlIgnore] public string FullTrackName { get; }
    [YamlIgnore] public CSPTrackOptions CSPTrackOptions { get; }
    [YamlIgnore] public string WelcomeMessage { get; } = "";
    public ACExtraConfiguration Extra { get; private set; } = new();
    [YamlIgnore] public CMContentConfiguration? ContentConfiguration { get; private set; }
    public string ServerVersion { get; }
    [YamlIgnore] public string? CSPExtraOptions { get; }
    [YamlIgnore] public string BaseFolder { get; }
    [YamlIgnore] public bool LoadPluginsFromWorkdir { get; }
    [YamlIgnore] public int RandomSeed { get; } = Random.Shared.Next();
    
    /*
     * Search paths are like this:
     *
     * When no options are set, all config files must be located in "cfg/".
     * WELCOME_MESSAGE path is relative to the working directory of the server.
     *
     * When "preset" is set, all configs must be located in "presets/<preset>/".
     * WELCOME_MESSAGE path must be relative to the preset folder.
     *
     * When "serverCfgPath" is set, server_cfg.ini will be loaded from the specified path.
     * All other configs must be located in the same folder.
     *
     * When "entryListPath" is set, it takes precedence and entry_list.ini will be loaded from the specified path.
     */
    public ACServerConfiguration(string preset, ConfigurationLocations locations, bool loadPluginsFromWorkdir)
    {
        BaseFolder = locations.BaseFolder;
        LoadPluginsFromWorkdir = loadPluginsFromWorkdir;
        
        Log.Debug("Loading server_cfg.ini from {Path}", locations.ServerCfgPath);
        if (!File.Exists(locations.ServerCfgPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(locations.ServerCfgPath)!);
            using var serverCfg = Assembly.GetExecutingAssembly().GetManifestResourceStream("AssettoServer.Assets.server_cfg.ini")!;
            using var outFile = File.Create(locations.ServerCfgPath);
            serverCfg.CopyTo(outFile);
        }
        Server = ServerConfiguration.FromFile(locations.ServerCfgPath);

        Log.Debug("Loading entry_list.ini from {Path}", locations.EntryListPath);
        if (!File.Exists(locations.EntryListPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(locations.EntryListPath)!);
            using var entryList = Assembly.GetExecutingAssembly().GetManifestResourceStream("AssettoServer.Assets.entry_list.ini")!;
            using var outFile = File.Create(locations.EntryListPath);
            entryList.CopyTo(outFile);
        }
        EntryList = EntryList.FromFile(locations.EntryListPath);

        ServerVersion = ThisAssembly.AssemblyInformationalVersion;
        FullTrackName = string.IsNullOrEmpty(Server.TrackConfig) ? Server.Track : Server.Track + "-" + Server.TrackConfig;
        CSPTrackOptions = CSPTrackOptions.Parse(Server.Track);

        string welcomeMessagePath = string.IsNullOrEmpty(preset) ? Server.WelcomeMessagePath : Path.Join(BaseFolder, Server.WelcomeMessagePath);
        if (File.Exists(welcomeMessagePath))
        {
            WelcomeMessage = File.ReadAllText(welcomeMessagePath);
        }
        else if(!string.IsNullOrEmpty(welcomeMessagePath))
        {
            Log.Warning("Welcome message not found at {Path}", Path.GetFullPath(welcomeMessagePath));
        }
        
        if (File.Exists(locations.CSPExtraOptionsPath))
        {
            CSPExtraOptions = File.ReadAllText(locations.CSPExtraOptionsPath);
        }

        var sessions = new List<SessionConfiguration>();

        if (Server.Practice != null)
        {
            Server.Practice.Id = 0;
            Server.Practice.Type = SessionType.Practice;
            sessions.Add(Server.Practice);
        }

        if (Server.Qualify != null)
        {
            Server.Qualify.Id = 1;
            Server.Qualify.Type = SessionType.Qualifying;
            sessions.Add(Server.Qualify);
        }

        if (Server.Race != null)
        {
            Server.Race.Id = 2;
            Server.Race.Type = SessionType.Race;
            sessions.Add(Server.Race);
        }

        Sessions = sessions;

        if (Server.MaxClients == 0)
        {
            Server.MaxClients = EntryList.Cars.Count;
        }

        LoadExtraConfig(locations.ExtraCfgPath);
        WriteReferenceExtraConfig(Path.Join(locations.BaseFolder, "extra_cfg.reference.yml"));
        ACExtraConfiguration.WriteSchema(Path.Join(locations.BaseFolder, "schema.json"));
        
        if (Extra is { EnableAi: true, AiParams.AutoAssignTrafficCars: true })
        {
            foreach (var entry in EntryList.Cars)
            {
                if (entry.Model.Contains("traffic"))
                {
                    entry.AiMode = AiMode.Fixed;
                }
            }
        }

        if (Extra.AiParams.AiPerPlayerTargetCount == 0)
        {
            Extra.AiParams.AiPerPlayerTargetCount = EntryList.Cars.Count(c => c.AiMode != AiMode.None);
        }

        if (Extra.AiParams.MaxAiTargetCount == 0)
        {
            Extra.AiParams.MaxAiTargetCount = EntryList.Cars.Count(c => c.AiMode == AiMode.None) * Extra.AiParams.AiPerPlayerTargetCount;
        }

        var validator = new ACServerConfigurationValidator();
        validator.ValidateAndThrow(this);
    }

    private void WriteReferenceExtraConfig(string path)
    {
        FileInfo? info = null;
        if (File.Exists(path))
        {
            info = new FileInfo(path);
            info.IsReadOnly = false;
        }

        using (var writer = File.CreateText(path))
        {
            writer.WriteLine($"# AssettoServer {ThisAssembly.AssemblyInformationalVersion} Reference Configuration");
            writer.WriteLine("# This file serves as an overview of all possible options with their default values.");
            writer.WriteLine("# It is NOT read by the server - edit extra_cfg.yml instead!");
            writer.WriteLine();

            ACExtraConfiguration.ReferenceConfiguration.ToStream(writer, true);
        }

        info ??= new FileInfo(path);
        info.IsReadOnly = true;
    }

    private void LoadExtraConfig(string path) {
        Log.Debug("Loading extra_cfg.yml from {Path}", path);
        
        if (!File.Exists(path))
        {
            new ACExtraConfiguration().ToFile(path);
        }
        
        Extra = ACExtraConfiguration.FromFile(path);

        if (Regex.IsMatch(Server.Name, @"x:\w+$"))
        {
            const string errorMsg =
                "Server details are configured via ID in server name. This interferes with native AssettoServer server details. More info: https://assettoserver.org/docs/common-configuration-errors#wrong-server-details";
            if (Extra.IgnoreConfigurationErrors.WrongServerDetails)
            {
                Log.Warning(errorMsg);
            }
            else
            {
                throw new ConfigurationException(errorMsg) { HelpLink = "https://assettoserver.org/docs/common-configuration-errors#wrong-server-details" };
            }
        }

        if (Extra.EnableServerDetails)
        {
            string cmContentPath = Path.Join(BaseFolder, "cm_content/content.json");
            if (File.Exists(cmContentPath))
            {
                ContentConfiguration = JsonConvert.DeserializeObject<CMContentConfiguration>(File.ReadAllText(cmContentPath));
            }
        }
    }

    private (PropertyInfo? Property, object Parent) GetNestedProperty(string key)
    {
        string[] path = key.Split('.');
            
        object parent = this;
        PropertyInfo? propertyInfo = null;

        foreach (string property in path)
        {
            propertyInfo = parent.GetType().GetProperty(property);
            if (propertyInfo == null) continue;
                
            var propertyType = propertyInfo.PropertyType;
            if (!propertyType.IsPrimitive && !propertyType.IsEnum && propertyType != typeof(string))
            {
                parent = propertyInfo.GetValue(parent)!;
            }
        }

        return (propertyInfo, parent);
    }

    public bool SetProperty(string key, string value)
    {
        (var propertyInfo, object? parent) = GetNestedProperty(key);

        if (propertyInfo == null)
            throw new ConfigurationException($"Could not find property {key}");

        bool ret = false;
        try
        {
            ret = propertyInfo.SetValueFromString(parent, value);
        }
        catch (TargetInvocationException) { }

        return ret;
    }
}
