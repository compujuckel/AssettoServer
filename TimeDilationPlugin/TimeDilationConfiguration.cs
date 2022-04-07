using JetBrains.Annotations;

namespace TimeDilationPlugin;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class TimeDilationConfiguration
{
    public List<LUTEntry>? LookupTable { get; set; }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public struct LUTEntry
{
    public double SunAngle { get; set; }
    public double TimeMult { get; set; }
}