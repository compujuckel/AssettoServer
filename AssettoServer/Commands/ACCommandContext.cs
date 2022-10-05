using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using Qmmands;
using System;
using System.Text;
using AssettoServer.Network.Rcon;
using Serilog;

namespace AssettoServer.Commands;

public sealed class ACCommandContext : CommandContext
{
    public ACServer Server { get; }
    public ACTcpClient? Client { get; }

    public RconClient? RconClient { get; }
    public int RconRequestId { get; }
    public StringBuilder? RconResponseBuilder { get; }

    private readonly EntryCarManager _entryCarManager;

    public ACCommandContext(ACServer server, ACTcpClient client, EntryCarManager entryCarManager, IServiceProvider? serviceProvider = null) : base(serviceProvider)
    {
        Server = server;
        Client = client;
        _entryCarManager = entryCarManager;
    }

    public ACCommandContext(ACServer server, RconClient client, int rconRequestId, EntryCarManager entryCarManager, IServiceProvider? serviceProvider = null) : base(serviceProvider)
    {
        Server = server;
        RconResponseBuilder = new StringBuilder();
        RconClient = client;
        RconRequestId = rconRequestId;
        _entryCarManager = entryCarManager;
    }

    public void Reply(string message)
    {
        Client?.SendPacket(new ChatMessage { SessionId = 255, Message = message });
        RconResponseBuilder?.AppendLine(message);
    }

    public void Broadcast(string message)
    {
        Log.Information("Broadcast: {Message}", message);
        RconResponseBuilder?.AppendLine(message);
        _entryCarManager.BroadcastPacket(new ChatMessage { SessionId = 255, Message = message });
    }

    internal void SendRconResponse()
    {
        if (RconClient == null || RconResponseBuilder == null) return;
            
        RconClient.SendPacket(new ResponseValuePacket
        {
            RequestId = RconRequestId,
            Body = RconResponseBuilder.ToString()
        });
    }
}
