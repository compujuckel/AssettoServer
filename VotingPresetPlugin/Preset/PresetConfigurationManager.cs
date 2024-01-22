using AssettoServer.Server.Plugin;

namespace VotingPresetPlugin.Preset;

public class PresetConfigurationManager
{
    public PresetConfiguration CurrentConfiguration { get; }
    public List<PresetConfiguration> AllConfigurations { get; }
    public List<PresetConfiguration> VotingConfigurations { get; }
    public List<PresetType> AllPresetTypes { get; }
    public List<PresetType> VotingPresetTypes { get; }

    public PresetConfigurationManager(VotingPresetConfiguration votingPresetConfiguration)
    {
        CurrentConfiguration = votingPresetConfiguration.Meta;

        var configs = new List<PresetConfiguration>();
        var directories = Directory.GetDirectories("presets");
        foreach (var dir in directories)
        {
            var path = Path.Join(dir, "plugin_voting_preset_cfg.yml");
            if(File.Exists(path))
                configs.Add(PresetConfiguration.FromFile(path));
        }

        AllConfigurations = configs;
        VotingConfigurations = configs.Where(c => c.Voting!.Enabled).ToList();

        AllPresetTypes = AllConfigurations.Select(x => x.ToPresetType()).ToList();
        VotingPresetTypes = VotingConfigurations.Select(x => x.ToPresetType()).ToList();
    }
}
