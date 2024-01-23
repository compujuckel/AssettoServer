namespace VotingPresetPlugin.Preset;

public class PresetType
{
    public required string Name { get; set; }
    public required string PresetFolder { get; set; }

    public bool Equals(PresetType compare) => PresetFolder == compare.PresetFolder;
    
    public PresetType(){}
}
