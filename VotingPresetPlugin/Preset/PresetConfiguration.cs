using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace VotingPresetPlugin.Preset;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class PresetConfiguration
{

    [YamlMember(Description = "The name that is displayed when a vote is going on or the preset is changing")]
    public string Name { get; set; } = "<Please change me>";

    [YamlMember(Description = "Whether only admins should be able to change to this preset")]
    public bool AdminOnly { get; set; } = false;
    
    
    [YamlIgnore] public string Path { get; set; } = "";
    
    public bool Equals(PresetConfiguration compare) => Path == compare.Path;

    public PresetType ToPresetType() => new PresetType
        {
            Name = Name,
            PresetFolder = Path
        };
    
    public static PresetConfiguration FromFile(string path)
    {
        using var reader = File.OpenText(path);
        var deserializer = new DeserializerBuilder().Build();
        var cfg = deserializer.Deserialize<VotingPresetConfiguration>(reader).Meta;

        cfg.Path = System.IO.Path.GetDirectoryName(path)!;
        
        return cfg;
    }
}
