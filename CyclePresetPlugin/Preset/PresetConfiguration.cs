using JetBrains.Annotations;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace CyclePresetPlugin.Preset;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class PresetConfiguration
{

    [YamlMember(Description = "The name that is displayed when a vote is going on or the preset is changing")]
    public string Name { get; set; } = "<Please change me>";

    [YamlMember(Description = "Preset specific settings for randomization")]
    public RandomPresetEntry Random { get; set; } = new();

    [YamlMember(Description = "Preset specific settings for voting")]
    public VotingPresetEntry Voting { get; set; } = new();
    
    
    [YamlIgnore] public string PresetFolder { get; set; } = "";
    [YamlIgnore] public string Path { get; set; } = "";
    
    public bool Equals(PresetConfiguration compare) => PresetFolder == compare.PresetFolder;

    public PresetType ToPresetType()
    {
        return new PresetType()
        {
            Name = Name,
            PresetFolder = PresetFolder,
            Weight = Random?.Weight ?? 1.0f,
        };
    }
    
    public static PresetConfiguration FromFile(string path)
    {
        using var reader = File.OpenText(path);
        var deserializer = new DeserializerBuilder().Build();
        var cfg = deserializer.Deserialize<CyclePresetConfiguration>(reader).Meta;

        cfg.Path = path;
        cfg.PresetFolder = System.IO.Path.GetDirectoryName(path)!;
        
        return cfg;
    }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class RandomPresetEntry
{
    [YamlMember(Description = "Is this preset part of the random selection")]
    public bool Enabled { get; set; } = false;
    
    [YamlMember(Description = "Weights for random preset selection, setting a weight to 0 blacklists a preset, default weight is 1")]
    public float Weight { get; set; } = 1.0f;
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class VotingPresetEntry
{
    [YamlMember(Description = "Is this preset part of the voting")]
    public bool Enabled { get; set; } = false;
}

