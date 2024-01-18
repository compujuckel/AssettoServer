using System.IO;
using JetBrains.Annotations;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Preset;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class PresetConfiguration
{
    public required string Name { get; set; }
    public RandomTrackPresetEntry? RandomTrack { get; set; }
    public VotingTrackPresetEntry? VotingTrack { get; set; }
    [YamlIgnore] public string PresetFolder { get; set; } = "";
    [YamlIgnore] public string Path { get; set; } = "";
    
    public bool Equals(PresetConfiguration compare) => PresetFolder == compare.PresetFolder;

    public PresetType ToPresetType()
    {
        return new PresetType()
        {
            Name = Name,
            PresetFolder = PresetFolder,
            Weight = RandomTrack?.Weight ?? 1.0f,
        };
    }
    
    public static PresetConfiguration FromFile(string path)
    {
        using var stream = File.OpenText(path);

        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();

        var yamlParser = new Parser(stream);
        yamlParser.Consume<StreamStart>();
        yamlParser.Accept<DocumentStart>(out _);

        var cfg = deserializer.Deserialize<PresetConfiguration>(yamlParser);

        cfg.Path = path;
        cfg.PresetFolder = System.IO.Path.GetDirectoryName(path)!;
        
        return cfg;
    }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class RandomTrackPresetEntry
{
    public bool Enabled { get; set; } = false;
    public float Weight { get; set; } = 1.0f;
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class VotingTrackPresetEntry
{
    public bool Enabled { get; set; } = false;
}

