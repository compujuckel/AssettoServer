using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AssettoServer.Server.Configuration.Extra;
using AssettoServer.Server.Configuration.Kunos;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Model;
using AssettoServer.Shared.Network.Http.Responses;
using AssettoServer.Utils;
using Autofac;
using FluentValidation;
using Newtonsoft.Json;
using Serilog;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Configuration;

public partial class ACServerConfiguration
{
    public ServerConfiguration Server { get; }
    public EntryList EntryList { get; }
    public List<SessionConfiguration> Sessions { get; }
    [YamlIgnore] public string FullTrackName { get; }
    [YamlIgnore] public CSPTrackOptions CSPTrackOptions { get; }
    [YamlIgnore] public string WelcomeMessage { get; }
    public ACExtraConfiguration Extra { get; private set; } = new();
    [YamlIgnore] public CMContentConfiguration? ContentConfiguration { get; }
    [YamlIgnore] public CMWrapperParams? WrapperParams { get; }
    public string ServerVersion { get; }
    [YamlIgnore] public string? CSPExtraOptions { get; }
    [YamlIgnore] public string BaseFolder { get; }
    [YamlIgnore] public bool LoadPluginsFromWorkdir { get; }
    [YamlIgnore] public bool GeneratePluginConfigs { get; }
    [YamlIgnore] public int RandomSeed { get; } = Random.Shared.Next();
    [YamlIgnore] public string? Preset { get; }
    [YamlIgnore] public DrsZones DrsZones { get; }    
    [YamlIgnore] public CarSetups Setups { get; }
    
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
    public ACServerConfiguration(string? preset, ConfigurationLocations locations, bool loadPluginsFromWorkdir, bool generatePluginConfigs, PortOverrides? portOverrides)
    {
        Preset = preset;
        BaseFolder = locations.BaseFolder;
        LoadPluginsFromWorkdir = loadPluginsFromWorkdir;
        GeneratePluginConfigs = generatePluginConfigs;
        Server = LoadServerConfiguration(locations.ServerCfgPath, portOverrides);
        EntryList = LoadEntryList(locations.EntryListPath);
        Setups = LoadSetups();
        WelcomeMessage = LoadWelcomeMessage();
        CSPExtraOptions = LoadCspExtraOptions(locations.CSPExtraOptionsPath);
        ContentConfiguration = LoadContentConfiguration(locations.CMContentJsonPath);
        WrapperParams = LoadCMWrapperParams(locations.CMWrapperParamsPath);
        ServerVersion = ThisAssembly.AssemblyInformationalVersion;
        Sessions = PrepareSessions();

        var extraCfgSchemaPath = ConfigurationSchemaGenerator.WriteExtraCfgSchema();
        LoadExtraConfig(locations.ExtraCfgPath, extraCfgSchemaPath);
        ReferenceConfigurationHelper.WriteReferenceConfiguration("extra_cfg.reference.yml",
            extraCfgSchemaPath, 
            ACExtraConfiguration.ReferenceConfiguration, 
            $"AssettoServer {ThisAssembly.AssemblyInformationalVersion}");
        
        var parsedTrackOptions = CSPTrackOptions = CSPTrackOptions.Parse(Server.Track);
        if (Extra.MinimumCSPVersion.HasValue
            && (!CSPTrackOptions.MinimumCSPVersion.HasValue || Extra.MinimumCSPVersion.Value > CSPTrackOptions.MinimumCSPVersion.Value))
        {
            CSPTrackOptions = new CSPTrackOptions
            {
                Track = parsedTrackOptions.Track,
                Flags = parsedTrackOptions.Flags,
                MinimumCSPVersion = Extra.MinimumCSPVersion
            };
            Server.Track = CSPTrackOptions.ToString();
        }
        else
        {
            CSPTrackOptions = parsedTrackOptions;
        }

        if (CSPTrackOptions.MinimumCSPVersion.HasValue)
        {
            Log.Debug("Using minimum required CSP Version {Version}", CSPTrackOptions.MinimumCSPVersion.Value);
        }

        FullTrackName = string.IsNullOrEmpty(Server.TrackConfig) ? Server.Track : $"{Server.Track}-{Server.TrackConfig}";
        DrsZones = LoadDrsZones(locations.DrsZonePath(CSPTrackOptions.Track, Server.TrackConfig), Extra.EnableGlobalDrs);
        
        ApplyConfigurationFixes();

        var validator = new ACServerConfigurationValidator();
        validator.ValidateAndThrow(this);
    }

    private ServerConfiguration LoadServerConfiguration(string path, PortOverrides? portOverrides)
    {
        Log.Debug("Loading server_cfg.ini from {Path}", path);
        try
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var serverCfg = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("AssettoServer.Assets.server_cfg.ini")!;
                using var outFile = File.Create(path);
                serverCfg.CopyTo(outFile);
            }

            var config = ServerConfiguration.FromFile(path);

            if (portOverrides != null)
            {
                config.TcpPort = portOverrides.TcpPort;
                config.UdpPort = portOverrides.UdpPort;
                config.HttpPort = portOverrides.HttpPort;
            }
            
            return config;
        }
        catch (Exception ex)
        {
            throw new ConfigurationParsingException(path, ex);
        }
    }

    private EntryList LoadEntryList(string path)
    {
        Log.Debug("Loading entry_list.ini from {Path}", path);
        try
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var entryList = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("AssettoServer.Assets.entry_list.ini")!;
                using var outFile = File.Create(path);
                entryList.CopyTo(outFile);
            }

            return EntryList.FromFile(path);
        }
        catch (Exception ex)
        {
            throw new ConfigurationParsingException(path, ex);
        }
    }
    
    private CarSetups LoadSetups()
    {
        CarSetups setups = new();

        foreach (var path in EntryList.Cars.Where(c => c.FixedSetup != null).Select(c => c.FixedSetup!).Distinct())
        {
            setups.Setups[path] = CarSetups.FromFile(Path.Join("setups", path));
        }

        return setups;
    }

    private static DrsZones LoadDrsZones(string path, bool global)
    {
        if (global)
            return new DrsZones
            {
                Zones = [
                    new DrsZones.DrsZone
                    {
                        Detection = 0.0f,
                        Start = 0.0f,
                        End = 1.0f,
                    }
                ]
            };
        try
        {
            return File.Exists(path) ? DrsZones.FromFile(path) : new DrsZones() ;
        }
        catch (Exception ex)
        {
            throw new ConfigurationParsingException(path, ex);
        }
    }

    private string LoadWelcomeMessage()
    {
        var welcomeMessage = "";
        var welcomeMessagePath = string.IsNullOrEmpty(Preset) ? Server.WelcomeMessagePath : Path.Join(BaseFolder, Server.WelcomeMessagePath);
        if (File.Exists(welcomeMessagePath))
        {
            welcomeMessage = File.ReadAllText(welcomeMessagePath);
        }
        else if(!string.IsNullOrEmpty(welcomeMessagePath))
        {
            Log.Warning("Welcome message not found at {Path}", Path.GetFullPath(welcomeMessagePath));
        }

        return welcomeMessage;
    }

    private static string? LoadCspExtraOptions(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static CMContentConfiguration? LoadContentConfiguration(string path)
    {
        CMContentConfiguration? contentConfiguration = null;
        if (File.Exists(path))
        {
            contentConfiguration = JsonConvert.DeserializeObject<CMContentConfiguration>(File.ReadAllText(path));
        }

        return contentConfiguration;
    }

    private static CMWrapperParams? LoadCMWrapperParams(string path)
    {
        CMWrapperParams? wrapperParams = null;
        if (File.Exists(path))
        {
            wrapperParams = JsonConvert.DeserializeObject<CMWrapperParams>(File.ReadAllText(path));
        }

        return wrapperParams;
    }

    private List<SessionConfiguration> PrepareSessions()
    {
        var sessions = new List<SessionConfiguration>();

        if (Server.Practice != null)
        {
            Server.Practice.Id = sessions.Count;
            Server.Practice.Type = SessionType.Practice;
            sessions.Add(Server.Practice);
        }

        if (Server.Qualify != null)
        {
            Server.Qualify.Id = sessions.Count;
            Server.Qualify.Type = SessionType.Qualifying;
            sessions.Add(Server.Qualify);
        }

        if (Server.Race != null)
        {
            Server.Race.Id = sessions.Count;
            Server.Race.Type = SessionType.Race;
            if (!Server.Race.IsTimedRace)
                Server.HasExtraLap = false;
            sessions.Add(Server.Race);
        }

        return sessions;
    }

    private void ApplyConfigurationFixes()
    {
        if (Server.MaxClients == 0)
        {
            Server.MaxClients = EntryList.Cars.Count;
        }
        
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
            Extra.AiParams.MaxAiTargetCount = EntryList.Cars.Count(c => c.AiMode != AiMode.Fixed) * Extra.AiParams.AiPerPlayerTargetCount;
        }
        
        var filteredServerName = ServerDetailsIdRegex().Replace(Server.Name, "");
        if (filteredServerName != Server.Name)
        {
            Extra.EnableServerDetails = true;
            Server.Name = filteredServerName;

            if (!string.IsNullOrEmpty(WrapperParams?.Description) && string.IsNullOrEmpty(Extra.ServerDescription))
            {
                Extra.ServerDescription = WrapperParams.Description;
            }
        }
    }

    internal void LoadPluginConfiguration(ACPluginLoader loader, ContainerBuilder? builder)
    {
        foreach (var plugin in loader.LoadedPlugins)
        {
            if (!plugin.HasConfiguration) continue;

            var configPath = Path.Join(BaseFolder, plugin.ConfigurationFileName);
            try
            {
                var schemaPath = ConfigurationSchemaGenerator.WritePluginConfigurationSchema(plugin);
                ReferenceConfigurationHelper.WriteReferenceConfiguration(plugin.ReferenceConfigurationFileName,
                    schemaPath, plugin.ReferenceConfiguration, plugin.Name);
                
                if (File.Exists(configPath) && builder != null)
                {
                    var deserializer = new DeserializerBuilder().Build();
                    using var file = File.OpenText(configPath);
                    var configObj = deserializer.Deserialize(file, plugin.ConfigurationType)!;

                    ValidatePluginConfiguration(plugin, configObj);
                    builder.RegisterInstance(configObj).AsSelf();
                }
                else
                {
                    var serializer = new SerializerBuilder().Build();
                    using var file = File.CreateText(configPath);
                    ConfigurationSchemaGenerator.WriteModeLine(file, BaseFolder, schemaPath);
                    var configObj = Activator.CreateInstance(plugin.ConfigurationType)!;
                    serializer.Serialize(file, configObj, plugin.ConfigurationType);
                }
            }
            catch (Exception ex)
            {
                throw new ConfigurationParsingException(configPath, ex);
            }
        }

        if (Extra.MandatoryClientSecurityLevel > 0 
            && loader.LoadedPlugins.All(plugin => plugin.Name != "ClientSecurityPlugin"))
        {
            Log.Warning("ClientSecurityPlugin not installed, setting MandatoryClientSecurityLevel to 0");
            Extra.MandatoryClientSecurityLevel = 0;
        }

        // Throw exception only after default plugin configs have been written
        if (Extra.ContainsObsoletePluginConfiguration)
        {
            throw new ConfigurationException(
                "Plugins are no longer configured via extra_cfg.yml. Please remove your plugin configuration from extra_cfg.yml and transfer it to the plugin-specific config files in your config folder.");
        }
    }

    private static void ValidatePluginConfiguration(LoadedPlugin plugin, object configuration)
    {
        if (plugin.ValidatorType == null) return;
        
        var validator = Activator.CreateInstance(plugin.ValidatorType)!;
        var method = typeof(DefaultValidatorExtensions).GetMethod(nameof(DefaultValidatorExtensions.ValidateAndThrow))!;
        var generic = method.MakeGenericMethod(configuration.GetType());
        try
        {
            generic.Invoke(null, [validator, configuration]);
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException ?? ex;
        }
    }

    private void LoadExtraConfig(string path, string schemaPath) {
        Log.Debug("Loading extra_cfg.yml from {Path}", path);

        try
        {
            if (!File.Exists(path))
            {
                using var file = File.CreateText(path);
                ConfigurationSchemaGenerator.WriteModeLine(file, BaseFolder, schemaPath);
                new ACExtraConfiguration().ToStream(file);
            }

            Extra = ACExtraConfiguration.FromFile(path);
        }
        catch (Exception ex)
        {
            throw new ConfigurationParsingException(path, ex);
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

    [GeneratedRegex(@"\s*x:\w+$")]
    private static partial Regex ServerDetailsIdRegex();
}
