using System;
using System.Text;
using AssettoServer.Network.Rcon;
using AssettoServer.Server;

namespace AssettoServer.Commands.Contexts;

public class RconCommandContext(
        EntryCarManager entryEntryCarManager,
        RconClient rconClient,
        int rconRequestId,
        IServiceProvider? serviceProvider = null)
    : BaseCommandContext(entryEntryCarManager, serviceProvider)
{
    public RconClient RconClient { get; } = rconClient;
    public int RconRequestId { get; } = rconRequestId;
    public StringBuilder RconResponseBuilder { get; } = new();

    public override bool IsAdministrator => true;

    public override void Reply(string message)
    {
        RconResponseBuilder.AppendLine(message);
    }
    
    public override void Broadcast(string message)
    {
        base.Broadcast(message);
        RconResponseBuilder.AppendLine(message);
    }
    
    internal void SendRconResponse()
    {
        RconClient.SendPacket(new ResponseValuePacket
        {
            RequestId = RconRequestId,
            Body = RconResponseBuilder.ToString()
        });
    }
}
