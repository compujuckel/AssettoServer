using System.Runtime.InteropServices.Marshalling;
using System.ServiceModel.Channels;
using System.Text.Json;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Configuration.Kunos;
using AssettoServer.Server.Plugin;
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

        var configs = new List<PresetConfiguration>();
        var directories = Directory.GetDirectories("presets");
        
        // don't ask, it's just for comparison ok
        var baseEntryList = JsonSerializer.Serialize(acServerConfiguration.EntryList);
        var warnEntryLists = false;
        foreach (var dir in directories)
        {
            var pluginCfgPath = Path.Join(dir, "plugin_voting_preset_cfg.yml");
            
            if (!File.Exists(pluginCfgPath)) continue;
            
            if (!votingPresetConfiguration.SkipEntryListCheck)
            {
                var entryListPath = Path.Join(dir, "entry_list.ini");
                
                if (!File.Exists(entryListPath))
                {
                    Log.Error("Preset {Preset} skipped, EntryList is missing", dir);
                    continue;
                }

                var compareEntryList = EntryList.FromFile(entryListPath);
                if (JsonSerializer.Serialize(compareEntryList) != baseEntryList)
                {
                    Log.Warning("Preset {Preset} skipped, EntryList does not match", dir);
                    warnEntryLists = true;
                    continue;
                }
            }
            if (warnEntryLists)
                Log.Warning("Mismatching EntryLists can cause issues with reconnecting");

            configs.Add(PresetConfiguration.FromFile(pluginCfgPath));
        }

        AllConfigurations = configs;
        VotingConfigurations = configs.Where(c => c.Voting!.Enabled).ToList();

        AllPresetTypes = AllConfigurations.Select(x => x.ToPresetType()).ToList();
        VotingPresetTypes = VotingConfigurations.Select(x => x.ToPresetType()).ToList();
        
        Log.Information("Number of presets loaded: {PresetCount}", configs.Count);
    }
}
