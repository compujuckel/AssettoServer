using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using Qmmands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace AssettoServer.Commands
{
    public sealed class ACCommandContext : CommandContext
    {
        public ACServer Server { get; }
        public ACTcpClient Client { get; }
        public ChatMessage Message { get; }
        public bool IsConsole => Client == null && Message.SessionId == 255;

        public ACCommandContext(ACServer server, ACTcpClient client, ChatMessage message, IServiceProvider serviceProvider = null) : base(serviceProvider)
        {
            Server = server;
            Client = client;
            Message = message;
        }

        public void Reply(string message)
        {
            if (IsConsole)
                Log.Information(message);
            else
                Client.SendPacket(new ChatMessage { SessionId = 255, Message = message });
        }

        public void Broadcast(string message)
        {
            Log.Information(message);
            Server.BroadcastPacket(new ChatMessage { SessionId = 255, Message = message });
        }
    }
}
