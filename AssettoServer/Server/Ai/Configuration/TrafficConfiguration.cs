using System.Collections.Generic;
using JetBrains.Annotations;

namespace AssettoServer.Server.Ai.Configuration;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class TrafficConfiguration
{
    public string? Track { get; set; }
    public string? Author { get; set; }
    public int? Version { get; set; }
    public List<SplineConfiguration> Splines { get; set; } = new();
}
