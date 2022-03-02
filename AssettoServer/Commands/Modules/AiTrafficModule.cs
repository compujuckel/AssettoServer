using AssettoServer.Commands.Attributes;
using JetBrains.Annotations;
using Qmmands;

namespace AssettoServer.Commands.Modules;

[RequireAdmin]
[RequireAiTraffic]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class AiTrafficModule : ACModuleBase
{
    [Command("setsplineheight")]
    public void SetSplineHeight(float height)
    {
        foreach (var entryCar in Context.Server.EntryCars)
        {
            entryCar.AiSplineHeightOffsetMeters = height;
        }
        
        Reply($"AI spline height set to {height}m");
    }

    [Command("setaioverbooking")]
    public void SetAiOverbooking(int count)
    {
        Context.Server.AiBehavior!.SetAiOverbooking(count);
        Reply($"AI overbooking set to {count}");
    }
}
