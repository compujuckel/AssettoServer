using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace TimeDilationPlugin;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class TimeDilationConfiguration : IValidateConfiguration<TimeDilationConfigurationValidator>
{
    [YamlMember(Description = "Table to map sun angles to time multipliers. SunAngle is the altitude of the sun in degrees. 90° = sun directly overhead, -90° = sun directly underneath")]
    public List<LUTEntry> LookupTable { get; set; } = null!;
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public struct LUTEntry
{
    public double SunAngle { get; set; }
    public double TimeMult { get; set; }
}
