using AssettoServer;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Network.Packets.Outgoing;
using Serilog;

namespace CyclePresetPlugin.Preset;

public class PresetImplementation
{
    private readonly ACServerConfiguration _acServerConfiguration;
    private readonly EntryCarManager _entryCarManager;
    
    private const string RestartKickReason = "SERVER RESTART FOR TRACK CHANGE (won't take long)";

    public PresetImplementation(ACServerConfiguration acServerConfiguration, EntryCarManager entryCarManager)
    {
        _acServerConfiguration = acServerConfiguration;
        _entryCarManager = entryCarManager;
    }

    public void ChangeTrack(PresetData presetData)
    {
        // Notify about restart
        Log.Information("Restarting server");
    
        if (_acServerConfiguration.Extra.EnableClientMessages)
        {
            // Reconnect clients
            Log.Information("Reconnecting all clients for preset change");
            _entryCarManager.BroadcastPacket(new ReconnectClientPacket { Time = (ushort) presetData.TransitionDuration });
        }
        else
        {
            Log.Information("Kicking all clients for track change server restart");
            _entryCarManager.BroadcastPacket(new CSPKickBanMessageOverride { Message = RestartKickReason });
            _entryCarManager.BroadcastPacket(new KickCar { SessionId = 255, Reason = KickReason.Kicked });
        }
        
        var preset = new DirectoryInfo(presetData.UpcomingType!.PresetFolder).Name;
        
        // Restart the server
        var sleep = (presetData.TransitionDuration - 1) * 1000;
        Thread.Sleep(sleep);
        
        Program.RestartServer(preset);
    }
}
