using AssettoServer.Utils;
using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration.Kunos;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class DynamicTrackConfiguration
{
    [IniField("SESSION_START", Percent = true)] public float BaseGrip { get; internal set; } = 1;
    [IniField("SESSION_TRANSFER", Percent = true)] public float TotalLapCount { get; internal set; }
    [IniField("LAP_GAIN")] public float GripPerLap { get; internal set; }
    [IniField("RANDOMNESS")] public float Randomness { get; internal set; }
}
