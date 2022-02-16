using System.Collections.Generic;
using JetBrains.Annotations;

namespace AssettoServer.Server.Ai;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class TrafficConfiguration
{
    public string? Track { get; set; }
    public string? Author { get; set; }
    public int? Version { get; set; }
    public List<SplineConfiguration> Splines { get; set; } = new();
}

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

public enum Indicator
{
    None,
    Left,
    Right
}
