using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace TrafficAiPlugin.Configuration;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class LaneCountSpecificOverrides
{
    [YamlMember(Description = "Minimum distance between AI cars")]
    public int MinAiSafetyDistanceMeters { get; set; }
    [YamlMember(Description = "Maximum distance between AI cars")]
    public int MaxAiSafetyDistanceMeters { get; set; }
    
    [YamlIgnore] public int MinAiSafetyDistanceSquared => MinAiSafetyDistanceMeters * MinAiSafetyDistanceMeters;
    [YamlIgnore] public int MaxAiSafetyDistanceSquared => MaxAiSafetyDistanceMeters * MaxAiSafetyDistanceMeters;
}
