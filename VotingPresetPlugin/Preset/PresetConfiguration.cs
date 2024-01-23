using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace VotingPresetPlugin.Preset;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class PresetConfiguration
{

    [YamlMember(Description = "The name that is displayed when a vote is going on or the preset is changing")]
    public string Name { get; set; } = "<Please change me>";

    [YamlMember(Description = "Is this preset part of the voting, Admins can still change to this track.")]
    public bool VotingEnabled { get; set; } = true;
    
    
    [YamlIgnore] public string PresetFolder { get; set; } = "";
    [YamlIgnore] public string Path { get; set; } = "";
    
    public bool Equals(PresetConfiguration compare) => PresetFolder == compare.PresetFolder;

    public PresetType ToPresetType() => new PresetType
        {
            Name = Name,
            PresetFolder = PresetFolder
        };
    
    public static PresetConfiguration FromFile(string path)
    {
        using var reader = File.OpenText(path);
        var deserializer = new DeserializerBuilder().Build();
        var cfg = deserializer.Deserialize<VotingPresetConfiguration>(reader).Meta;

        cfg.Path = path;
        cfg.PresetFolder = System.IO.Path.GetDirectoryName(path)!;
        
        return cfg;
    }
}
