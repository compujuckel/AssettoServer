using AssettoServer.Server.Configuration;
using JetBrains.Annotations;

namespace TimeDilationPlugin;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class TimeDilationConfiguration : IValidateConfiguration<TimeDilationConfigurationValidator>
{
    public List<LUTEntry> LookupTable { get; set; } = null!;
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public struct LUTEntry
{
    public double SunAngle { get; set; }
    public double TimeMult { get; set; }
}
