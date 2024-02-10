using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace TimeDilationPlugin;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class TimeDilationConfiguration : IValidateConfiguration<TimeDilationConfigurationValidator>
{
    [YamlMember(Description = "Which mode should be used for time dilation \nAvailable values: 'SunAngle' and 'Time'")]
    public TimeDilationMode Mode = TimeDilationMode.SunAngle;
    [YamlMember(Description =
        "Table to map sun angles to time multipliers. SunAngle is the altitude of the sun in degrees. 90° = sun directly overhead, -90° = sun directly underneath")]
    public List<SALUTEntry> SunAngleLookupTable { get; set; } =
    [
        new SALUTEntry { SunAngle = 6, TimeMult = 4 },
        new SALUTEntry { SunAngle = 12, TimeMult = 8 }
    ];
    
    [YamlMember(Description =
        "Table to map time to time multipliers. For time use format 24-hour format without leading zeros. For example 6:00 or 15:30")]
    public List<TLUTEntry> TimeLookupTable { get; set; } =
    [
        new TLUTEntry { Time = "4:20", TimeMult = 6 },
        new TLUTEntry { Time = "19:00", TimeMult = 9 }
    ];
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class SALUTEntry
{
    public double SunAngle { get; set; }
    public double TimeMult { get; set; }
}


[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class TLUTEntry
{
    public string Time { get; set; }
    public double TimeMult { get; set; }
}

public enum TimeDilationMode
{
    SunAngle,
    Time
}
