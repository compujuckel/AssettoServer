using JetBrains.Annotations;

namespace TrafficAiPlugin.Configuration;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class JunctionRecord
{
    public string Name { get; set; } = "";
    public int Start { get; set; }
    public string End { get; set; } = "";
    public float Probability { get; set; }
    public Indicator IndicateWhenTaken { get; set; } = Indicator.None;
    public Indicator IndicateWhenNotTaken { get; set; } = Indicator.None;
    public float IndicateDistancePre { get; set; } = 75;
    public float IndicateDistancePost { get; set; } = 50;
}
