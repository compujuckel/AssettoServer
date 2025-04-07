using AssettoServer.Network.ClientMessages;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Services;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using TrafficAiPlugin.Packets;
using TrafficAiPlugin.Shared;

namespace TrafficAiPlugin;

using TrafficAiConfiguration = Configuration.TrafficAiConfiguration;

public class TrafficAi : CriticalBackgroundService, IAssettoServerAutostart, ITrafficAi
{
    private readonly TrafficAiConfiguration _configuration;
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _sessionManager;
    private readonly Func<EntryCar, EntryCarTrafficAi> _entryCarTrafficAiFactory;

    public readonly List<EntryCarTrafficAi> Instances = [];

    public TrafficAi(TrafficAiConfiguration configuration,
        ACServerConfiguration serverConfiguration,
        EntryCarManager entryCarManager,
        SessionManager sessionManager,
        Func<EntryCar, EntryCarTrafficAi> entryCarTrafficAiFactory,
        CSPClientMessageTypeManager cspClientMessageTypeManager,
        IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _configuration = configuration;
        _serverConfiguration = serverConfiguration;
        _entryCarManager = entryCarManager;
        _sessionManager = sessionManager;
        _entryCarTrafficAiFactory = entryCarTrafficAiFactory;

        _configuration.ApplyConfigurationFixes(_serverConfiguration);
        
        if (_configuration.EnableCarReset)
        {
            if (!_serverConfiguration.Extra.EnableClientMessages || _serverConfiguration.CSPTrackOptions.MinimumCSPVersion < CSPVersion.V0_2_3_p47)
            {
                throw new ConfigurationException(
                    "Reset car: Minimum required CSP version of 0.2.3-preview47 (2796); Requires enabled client messages; Requires working AI spline");
            }
            cspClientMessageTypeManager.RegisterOnlineEvent<RequestResetPacket>((client, _) => { OnResetCar(client); });
        }

        _entryCarManager.ClientConnected += (sender, _) =>
        {
            if (_configuration.HideAiCars)
            {
                sender.FirstUpdateSent += OnFirstUpdateSentHideCars;
            }
            sender.HandshakeAccepted += OnHandshakeAccepted;
        };
    }

    private void OnHandshakeAccepted(ACTcpClient sender, HandshakeAcceptedEventArgs args)
    {
        // Gracefully despawn AI cars
        GetAiCarBySessionId(sender.SessionId).SetAiOverbooking(0);
    }

    private void OnFirstUpdateSentHideCars(ACTcpClient sender, EventArgs args)
    {
        sender.SendPacket(new CSPCarVisibilityUpdate
        {
            SessionId = sender.SessionId,
            Visible = sender.EntryCar.AiControlled ? CSPCarVisibility.Invisible : CSPCarVisibility.Visible
        });
    }

    private void OnResetCar(ACTcpClient sender)
    {
        if (_configuration.EnableCarReset)
            GetAiCarBySessionId(sender.SessionId).TryResetPosition();
    }

    // public EntryCarTrafficAi GetAiCarBySessionId(byte sessionId)
    //     => Instances.First(x => x.EntryCar.SessionId == sessionId);

    IEntryCarTrafficAi ITrafficAi.GetAiCarBySessionId(byte sessionId)
        => GetAiCarBySessionId(sessionId);

    internal EntryCarTrafficAi GetAiCarBySessionId(byte sessionId)
        => Instances.First(x => x.EntryCar.SessionId == sessionId);
    
    public float GetLaneWidthMeters()
        => _configuration.LaneWidthMeters;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var car in _entryCarManager.EntryCars)
        {
            var entry = _serverConfiguration.EntryList.Cars[car.SessionId];
            
            if (_configuration.AutoAssignTrafficCars && entry.Model.Contains("traffic"))
            {
                entry.AiMode = AiMode.Fixed;
            }
            
            car.AiMode = entry.AiMode;
            car.AiControlled = entry.AiMode != AiMode.None;
            
            Instances.Add(_entryCarTrafficAiFactory(car));
        }
    }
}
