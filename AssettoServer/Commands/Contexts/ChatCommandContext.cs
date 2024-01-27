using System;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Shared.Network.Packets.Shared;

namespace AssettoServer.Commands.Contexts;

public class ChatCommandContext(
        ACTcpClient client,
        EntryCarManager entryEntryCarManager,
        IServiceProvider? serviceProvider = null)
    : BaseCommandContext(entryEntryCarManager, serviceProvider)
{
    public ACTcpClient Client { get; } = client;

    public override bool IsAdministrator => Client.IsAdministrator;

    public override void Reply(string message)
    {
        Client.SendPacket(new ChatMessage { SessionId = 255, Message = message });
    }
}
