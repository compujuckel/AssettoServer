using AssettoServer.Shared.Model;
using AssettoServer.Utils;
using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration.Kunos;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SessionConfiguration : Session
{
    [IniField("NAME")] public override string? Name { get; set; } = "Default";
    [IniField("TIME")] public override int Time { get; set; } = 60;
    [IniField("LAPS")] public override int Laps { get; set; }
    [IniField("WAIT_TIME")] public uint WaitTime { get; set; }
    [IniField("IS_OPEN")] public IsOpenMode IsOpen { get; set; }
    [IniField("INFINITE")] public bool Infinite { get; set; }
    public bool IsTimedRace => Time > 0 && Laps == 0;
}

public enum IsOpenMode : ushort
{
    Closed = 0,
    Open = 1,
    CloseAtStart = 2
}
