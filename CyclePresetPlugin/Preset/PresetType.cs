namespace CyclePresetPlugin.Preset;

public class PresetType
{
    public required string Name { get; set; }
    public required string PresetFolder { get; set; }
    public float Weight { get; set; } = 1.0f;

    public bool Equals(PresetType compare) => PresetFolder == compare.PresetFolder;
    
    public PresetType(){}
}
