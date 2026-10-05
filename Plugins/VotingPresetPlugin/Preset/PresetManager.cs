using AssettoServer;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Network.Packets.Outgoing;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace VotingPresetPlugin.Preset;

public class PresetManager : BackgroundService
{
    private readonly ACServerConfiguration _acServerConfiguration;
    private readonly VotingPresetConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private bool _presetChangeRequested = false;
    
    private const string RestartKickReason = "SERVER RESTART FOR TRACK CHANGE (won't take long)";

    public PresetManager(ACServerConfiguration acServerConfiguration, 
        VotingPresetConfiguration configuration,
        EntryCarManager entryCarManager)
    {
        _acServerConfiguration = acServerConfiguration;
        _configuration = configuration;
        _entryCarManager = entryCarManager;
    }

    public PresetData CurrentPreset { get; private set; } = null!;

    public void SetPreset(PresetData preset)
    {
        CurrentPreset = preset;
        _presetChangeRequested = true;

        if (!CurrentPreset.IsInit)
            _ = UpdatePreset();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_presetChangeRequested)
                    await UpdatePreset();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in preset service update");
            }
            finally
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task UpdatePreset()
    {
        if (CurrentPreset.UpcomingType != null && !CurrentPreset.Type!.Equals(CurrentPreset.UpcomingType!))
        {
            Log.Information("Preset change to \'{Name}\' initiated", CurrentPreset.UpcomingType!.Name);
            
            Log.Information("Restarting server");
    
            if (_acServerConfiguration.Extra.EnableClientMessages && _configuration.EnableReconnect)
            {
                Log.Information("Reconnecting all clients for preset change");
                _entryCarManager.BroadcastPacket(new ReconnectClientPacket { Time = (ushort) CurrentPreset.TransitionDuration });
            }
            else
            {
                Log.Information("Kicking all clients for preset change, server restart");
                _entryCarManager.BroadcastPacket(new CSPKickBanMessageOverride { Message = RestartKickReason });
                _entryCarManager.BroadcastPacket(new KickCar { SessionId = 255, Reason = KickReason.Kicked });
            }

            var preset = new DirectoryInfo(CurrentPreset.UpcomingType!.PresetFolder).Name;
        
            // The minus 1 makes it so the server restarts 1 second before the reconnecting through script happens
            // Could probably be refined, but should suffice
            var sleep = (CurrentPreset.TransitionDuration - 1) * 1000;
            await Task.Delay(sleep);

            Program.RestartServer(
                preset,
                portOverrides: new PortOverrides
                {
                    TcpPort = _acServerConfiguration.Server.TcpPort,
                    UdpPort = _acServerConfiguration.Server.UdpPort,
                    HttpPort =  _acServerConfiguration.Server.HttpPort
                });
        }
    }
}
