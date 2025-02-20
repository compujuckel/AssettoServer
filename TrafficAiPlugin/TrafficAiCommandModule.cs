using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Utils;
using JetBrains.Annotations;
using Qmmands;
using TrafficAIPlugin.Configuration;

namespace TrafficAIPlugin;

[RequireAdmin]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class TrafficAiCommandModule : ACModuleBase
{
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly TrafficAiConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    
    public TrafficAiCommandModule(ACServerConfiguration serverConfiguration, TrafficAiConfiguration configuration, EntryCarManager entryCarManager)
    {
        _serverConfiguration = serverConfiguration;
        _configuration = configuration;
        _entryCarManager = entryCarManager;
    }

    [Command("setaioverbooking")]
    public void SetAiOverbooking(int count)
    {
        foreach (var aiCar in _entryCarManager.EntryCars.Where(car => car.AiControlled && car.Client == null))
        {
            aiCar.SetAiOverbooking(count);
        }
        Reply($"AI overbooking set to {count}");
    }
    
    [Command("resetcar"), RequireConnectedPlayer]
    public void ResetCarAsync()
    {
        if (_serverConfiguration.Extra is { EnableClientMessages: true, MinimumCSPVersion: >= CSPVersion.V0_2_3_p47 } &&
            _configuration.EnableCarReset)
        {
            Reply(Client!.EntryCar.TryResetPosition()
                ? "Position successfully reset"
                : "Couldn't reset position");
        }
        else
            Reply("Reset is not enabled on this server");
    }
}
