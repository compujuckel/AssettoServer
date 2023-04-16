using AssettoServer.Shared.Model;
using AssettoServer.Utils;
using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SessionConfiguration : ISession
{
    public int Id { get; set; }
    public SessionType Type { get; set; }
    [IniField("NAME")] public string Name { get; set; } = "";
    [IniField("TIME")] public uint Time { get; set; }
    [IniField("LAPS")] public uint Laps { get; set; }
    [IniField("WAIT_TIME")] public uint WaitTime { get; set; }
    [IniField("IS_OPEN")] public bool IsOpen { get; set; }
    public bool IsTimedRace => Time > 0 && Laps == 0;
}
