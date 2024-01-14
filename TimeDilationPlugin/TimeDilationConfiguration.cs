using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace TimeDilationPlugin;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class TimeDilationConfiguration : IValidateConfiguration<TimeDilationConfigurationValidator>
{
    [YamlMember(Description =
        "Table to map sun angles to time multipliers. SunAngle is the altitude of the sun in degrees. 90° = sun directly overhead, -90° = sun directly underneath")]
    public List<LUTEntry> LookupTable { get; set; } =
    [
        new LUTEntry { SunAngle = 6, TimeMult = 4 },
        new LUTEntry { SunAngle = 12, TimeMult = 8 }
    ];
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class LUTEntry
{
    public double SunAngle { get; set; }
    public double TimeMult { get; set; }
}
