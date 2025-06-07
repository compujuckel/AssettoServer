using System;
using AssettoServer.Utils;
using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration.Kunos;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class DynamicTrackConfiguration
{
    [IniField("SESSION_START", Percent = true)] public float StartGrip { get; internal set; } = 1;
    [IniField("SESSION_TRANSFER", Percent = true)] public float SessionTransfer { get; internal set; } = 1;
    // how many laps are needed to add 1% grip
    [IniField("LAP_GAIN")] public float LapGain { get; internal set; }
    [IniField("RANDOMNESS")] public float Randomness { get; init; }

    private float _variance = (float) (Random.Shared.NextDouble() * 2 - 1) / 100;
    
    public float BaseGrip => Math.Min(StartGrip + _variance * Randomness, 1);
    public float TotalLapCount { get; internal set; }
    private float GripPerLap => 1 / (LapGain * 100);
    public float? OverrideGrip { get; set; } = null;

    public float CurrentGrip => OverrideGrip ?? (LapGain == 0 ? BaseGrip : Math.Min(BaseGrip + GripPerLap * TotalLapCount, 1));
    public void TransferSession()
    {
        TotalLapCount *= SessionTransfer;
    }
}
