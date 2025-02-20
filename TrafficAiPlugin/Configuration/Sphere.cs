using System.Numerics;
using JetBrains.Annotations;

namespace TrafficAIPlugin.Configuration;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class Sphere
{
    public Vector3 Center { get; set; }
    public float RadiusMeters { get; set; }
}
