using AssettoServer.Shared.Model;
using AssettoServer.Utils;
using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SessionConfiguration : Session
{
    [IniField("NAME")] public override string? Name { get; set; } = "";
    [IniField("TIME")] public override int Time { get; set; }
    [IniField("LAPS")] public override int Laps { get; set; }
    [IniField("WAIT_TIME")] public uint WaitTime { get; set; }
    [IniField("IS_OPEN")] public bool IsOpen { get; set; }
    [IniField("INFINITE")] public bool Infinite { get; set; }
    public bool IsTimedRace => Time > 0 && Laps == 0;
}
