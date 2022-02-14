using System.Collections.Generic;
using JetBrains.Annotations;

namespace AssettoServer.Server.Ai;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class TrafficConfiguration
{
    public List<SplineConfiguration> Splines { get; set; } = new();
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SplineConfiguration
{
    public string Name { get; set; } = "";
    public string? ConnectEnd { get; set; }
    public List<JunctionRecord> Junctions { get; set; } = new();
}

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class JunctionRecord
{
    public string Name { get; set; } = "";
    public int Start { get; set; }
    public string End { get; set; } = "";
    public float Probability { get; set; }
}
