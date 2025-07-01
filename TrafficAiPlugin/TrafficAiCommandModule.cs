using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Utils;
using JetBrains.Annotations;
using Qmmands;
using TrafficAiPlugin.Configuration;

namespace TrafficAiPlugin;

[RequireAdmin]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class TrafficAiCommandModule : ACModuleBase
{
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly TrafficAiConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly TrafficAi _trafficAi;

    public TrafficAiCommandModule(ACServerConfiguration serverConfiguration,
        TrafficAiConfiguration configuration,
        EntryCarManager entryCarManager,
        TrafficAi trafficAi)
    {
        _serverConfiguration = serverConfiguration;
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _trafficAi = trafficAi;
    }

    [Command("setaioverbooking")]
    public void SetAiOverbooking(int count)
    {
        foreach (var aiCar in _trafficAi.Instances.Where(car => car.EntryCar is { AiControlled: true, Client: null }))
        {
            aiCar.SetAiOverbooking(count);
        }
        Reply($"AI overbooking set to {count}");
    }
    
    [Command("resetcar"), RequireConnectedPlayer]
    public void ResetCarAsync()
    {
        if (_serverConfiguration.Extra is { EnableClientMessages: true, MinimumCSPVersion: >= CSPVersion.V0_2_8 } &&
            _configuration.EnableCarReset)
        {
            Reply(_trafficAi.GetAiCarBySessionId(Client!.SessionId).TryResetPosition()
                ? "Position successfully reset"
                : "Couldn't reset position");
        }
        else
            Reply("Reset is not enabled on this server");
    }
}
