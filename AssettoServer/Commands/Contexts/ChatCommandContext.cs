using System;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;

namespace AssettoServer.Commands.Contexts;

public class ChatCommandContext(
        ACTcpClient client,
        EntryCarManager entryCarManager,
        IServiceProvider? serviceProvider = null)
    : BaseCommandContext(entryCarManager, serviceProvider)
{
    public ACTcpClient Client { get; } = client;

    public override bool IsAdministrator => Client.IsAdministrator;

    public override void Reply(string message)
    {
        Client.SendChatMessage(message);
    }
}
