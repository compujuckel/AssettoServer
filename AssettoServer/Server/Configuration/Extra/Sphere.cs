using System.Numerics;
using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration.Extra;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class Sphere
{
    public Vector3 Center { get; set; }
    public float RadiusMeters { get; set; }
}
