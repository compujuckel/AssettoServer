using JetBrains.Annotations;

namespace TrafficAIPlugin.Configuration;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SplineConfiguration
{
    public string Name { get; set; } = "";
    public string? ConnectEnd { get; set; }
    public Indicator IndicateEnd { get; set; } = Indicator.None;
    public float IndicateEndDistancePre { get; set; } = 150;
    public float IndicateEndDistancePost { get; set; } = 10;
    public List<JunctionRecord> Junctions { get; set; } = new();
}
