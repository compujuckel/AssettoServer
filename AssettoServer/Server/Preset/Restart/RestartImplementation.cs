using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Network.Packets.Outgoing;
using Serilog;

namespace AssettoServer.Server.Preset.Restart;

public class RestartImplementation
{
    private readonly ACServerConfiguration _acServerConfiguration;
    private readonly EntryCarManager _entryCarManager;

    
    /// <summary>
    /// It's public so plugins could change the kick reason
    /// </summary>
    public string RestartKickReason { get; set; } = "SERVER RESTART FOR TRACK CHANGE (won't take long)";

    public RestartImplementation(ACServerConfiguration acServerConfiguration, EntryCarManager entryCarManager)
    {
        _acServerConfiguration = acServerConfiguration;
        _entryCarManager = entryCarManager;
    }

    public void InitiateRestart(string presetPath, ushort time = 10)
    {
        if (_acServerConfiguration.Extra.EnableClientMessages)
        {
            // Reconnect clients
            Log.Information("Reconnecting all clients for preset change.");
            _entryCarManager.BroadcastPacket(new ReconnectClientPacket { Time = time });
        }
        else
        {
            Log.Information($"Kicking all clients for track change server restart.");
            _entryCarManager.BroadcastPacket(new CSPKickBanMessageOverride { Message = RestartKickReason });
            _entryCarManager.BroadcastPacket(new KickCar { SessionId = 255, Reason = KickReason.Kicked });
        }
        
        var preset = new DirectoryInfo(presetPath).Name;
        
        // Restart the server
        _acServerConfiguration.RestartCallBack(preset);
    }
}
