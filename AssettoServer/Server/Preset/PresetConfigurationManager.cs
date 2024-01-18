using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssettoServer.Server.Configuration;

namespace AssettoServer.Server.Preset;

public class PresetConfigurationManager
{
    public PresetConfiguration CurrentConfiguration { get; }
    public List<PresetConfiguration> AllConfigurations { get; }
    public List<PresetConfiguration> RandomConfigurations { get; }
    public List<PresetConfiguration> VotingConfigurations { get; }
    public List<PresetType> AllPresetTypes { get; }
    public List<PresetType> RandomPresetTypes { get; }
    public List<PresetType> VotingPresetTypes { get; }

    public PresetConfigurationManager(ACServerConfiguration acServerConfiguration)
    {
        CurrentConfiguration = PresetConfiguration.FromFile(Path.Join(acServerConfiguration.BaseFolder, "preset_cfg.yml"));

        var configs = new List<PresetConfiguration>();
        var directories = Directory.GetDirectories("presets");
        foreach (var dir in directories)
        {
            configs.Add(PresetConfiguration.FromFile(Path.Join(dir, "preset_cfg.yml")));
        }

        AllConfigurations = configs;
        RandomConfigurations = configs.Where(c => c.RandomTrack!.Enabled).ToList();
        VotingConfigurations = configs.Where(c => c.VotingTrack!.Enabled).ToList();

        var types = new List<PresetType>();
        foreach (var conf in AllConfigurations)
        {
            types.Add(conf.ToPresetType());
        }

        AllPresetTypes = types;
        RandomPresetTypes = configs.Where(c => c.RandomTrack!.Enabled).Select(x => x.ToPresetType()).ToList();
        VotingPresetTypes = configs.Where(c => c.VotingTrack!.Enabled).Select(x => x.ToPresetType()).ToList();
    }
}
