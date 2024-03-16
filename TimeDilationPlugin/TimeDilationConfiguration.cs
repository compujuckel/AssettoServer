using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace TimeDilationPlugin;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class TimeDilationConfiguration : IValidateConfiguration<TimeDilationConfigurationValidator>
{
    [YamlMember(Description = "Which mode should be used for time dilation \nAvailable values: 'SunAngle' and 'Time'\nSunAngle is preferred because it works independent of seasons and longitude of the track")]
    public TimeDilationMode Mode { get; set; } = TimeDilationMode.SunAngle;
    [YamlMember(Description =
        "Table to map sun angles to time multipliers. SunAngle is the altitude of the sun in degrees. 90° = sun directly overhead, -90° = sun directly underneath")]
    public List<SunAngleLUTEntry> SunAngleLookupTable { get; set; } =
    [
        new SunAngleLUTEntry { SunAngle = 6, TimeMult = 4 },
        new SunAngleLUTEntry { SunAngle = 12, TimeMult = 8 }
    ];
    
    [YamlMember(Description =
        "Table to map time to time multipliers. For time use format 24-hour format without leading zeros. For example 6:00 or 15:30")]
    public List<TimeLUTEntry> TimeLookupTable { get; set; } =
    [
        new TimeLUTEntry { Time = "4:20", TimeMult = 6 },
        new TimeLUTEntry { Time = "19:00", TimeMult = 9 }
    ];
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class SunAngleLUTEntry
{
    public double SunAngle { get; set; }
    public double TimeMult { get; set; }
}


[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class TimeLUTEntry
{
    public required string Time { get; set; }
    public double TimeMult { get; set; }
}

public enum TimeDilationMode
{
    SunAngle,
    Time
}
