using System.Linq;
using AssettoServer.Commands.Attributes;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using Qmmands;

namespace AssettoServer.Commands.Modules;

[RequireAdmin]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class AiTrafficModule : ACModuleBase
{
    private readonly ACServerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    
    public AiTrafficModule(ACServerConfiguration configuration, EntryCarManager entryCarManager)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
    }

    [Command("setaioverbooking")]
    public void SetAiOverbooking(int count)
    {
        if (!_configuration.Extra.EnableAi)
        {
            Reply("AI disabled");
            return;
        }
        
        foreach (var entryCar in _entryCarManager.EntryCars.Where(car => car is EntryCar { AiControlled: true, Client: null }))
        {
            var aiCar = (EntryCar)entryCar;
            aiCar.SetAiOverbooking(count);
        }
        Reply($"AI overbooking set to {count}");
    }
}
