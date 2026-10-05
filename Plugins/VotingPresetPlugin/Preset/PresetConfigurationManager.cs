using System.IO.Hashing;
using AssettoServer.Server.Configuration;
using Serilog;

namespace VotingPresetPlugin.Preset;

public class PresetConfigurationManager
{
    public PresetConfiguration CurrentConfiguration { get; }
    
    public List<PresetConfiguration> AllConfigurations { get; }
    public List<PresetConfiguration> VotingConfigurations { get; }
    public List<PresetType> AllPresetTypes { get; }
    public List<PresetType> VotingPresetTypes { get; }

    public PresetConfigurationManager(VotingPresetConfiguration votingPresetConfiguration, ACServerConfiguration acServerConfiguration)
    {
        CurrentConfiguration = votingPresetConfiguration.Meta;
        CurrentConfiguration.Path = acServerConfiguration.BaseFolder;

        var configs = new List<PresetConfiguration>();
        var directories = Directory.GetDirectories("presets");
        
        var baseEntryListHash = HashEntryList(acServerConfiguration.BaseFolder);
        foreach (var dir in directories)
        {
            var pluginCfgPath = Path.Join(dir, "plugin_voting_preset_cfg.yml");
            
            if (!File.Exists(pluginCfgPath)) continue;
            
            if (!votingPresetConfiguration.SkipEntryListCheck)
            {
                if (!File.Exists(Path.Join(dir, "entry_list.ini")))
                {
                    Log.Error("Preset {Preset} skipped, EntryList is missing", dir);
                    continue;
                }

                if (HashEntryList(dir) != baseEntryListHash)
                {
                    Log.Warning("Preset {Preset} skipped, EntryList does not match", dir);
                    continue;
                }
            }

            configs.Add(PresetConfiguration.FromFile(pluginCfgPath));
        }

        if (votingPresetConfiguration.SkipEntryListCheck)
        {
            Log.Warning("Mismatching EntryLists can cause issues with reconnecting");
        }
        
        if (configs.Count < 2)
        {
            throw new ConfigurationException(
                "VotingPresetPlugin needs a minimum of 2 presets");
        }
        
        AllConfigurations = configs;
        VotingConfigurations = configs.Where(c => !c.AdminOnly).ToList();

        AllPresetTypes = AllConfigurations.Select(x => x.ToPresetType()).ToList();
        VotingPresetTypes = VotingConfigurations.Select(x => x.ToPresetType()).ToList();
        
        Log.Information("Number of presets loaded: {PresetCount}", configs.Count);
        foreach (var preset in configs)
        {
            Log.Information("Loaded {PresetName} ({PresetPath})", preset.Name, preset.Path);
        }
    }

    private ulong HashEntryList(string path)
    {
        var hash = new XxHash64();
        
        string entryListPath = Path.Join(path, "entry_list.ini");
        using var stream = File.OpenRead(entryListPath);
        hash.Append(stream);
        return hash.GetCurrentHashAsUInt64();
    }
}
