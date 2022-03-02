using AssettoServer.Utils;
using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SessionConfiguration
{
    public int Id { get; internal set; }
    public SessionType Type => (SessionType)Id + 1;
    [IniField("NAME")] public string Name { get; init; } = "";
    [IniField("TIME")] public int Time { get; init; }
    [IniField("LAPS")] public int Laps { get; init; }
    [IniField("WAIT_TIME")] public int WaitTime { get; init; }
    [IniField("IS_OPEN")] public bool IsOpen { get; init; }
    public bool IsTimedRace => Time > 0 && Laps == 0;
}

public enum SessionType : byte
{
    Booking = 0,
    Practice,
    Qualifying,
    Race
}
