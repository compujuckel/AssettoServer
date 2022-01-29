using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using Qmmands;
using System;
using Serilog;

namespace AssettoServer.Commands
{
    public sealed class ACCommandContext : CommandContext
    {
        public ACServer Server { get; }
        public ACTcpClient Client { get; }
        public ChatMessage Message { get; }

        public ACCommandContext(ACServer server, ACTcpClient client, ChatMessage message, IServiceProvider? serviceProvider = null) : base(serviceProvider)
        {
            Server = server;
            Client = client;
            Message = message;
        }

        public void Reply(string message)
        {
            Client?.SendPacket(new ChatMessage { SessionId = 255, Message = message });
        }

        public void Broadcast(string message)
        {
            Log.Information("{0}", message);
            Server.BroadcastPacket(new ChatMessage { SessionId = 255, Message = message });
        }
    }
}
