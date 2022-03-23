using System.Linq;
using AssettoServer.Commands.Attributes;
using JetBrains.Annotations;
using Qmmands;

namespace AssettoServer.Commands.Modules;

[RequireAdmin]
[RequireAiTraffic]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class AiTrafficModule : ACModuleBase
{
    [Command("setaioverbooking")]
    public void SetAiOverbooking(int count)
    {
        foreach (var aiCar in Context.Server.EntryCars.Where(car => car.AiControlled && car.Client == null))
        {
            aiCar.SetAiOverbooking(count);
        }
        Reply($"AI overbooking set to {count}");
    }
}
