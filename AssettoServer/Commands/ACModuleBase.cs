using AssettoServer.Network.Packets.Shared;
using Qmmands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Commands
{
    public class ACModuleBase : ModuleBase<ACCommandContext>
    {
        public bool IsConsole => Context.IsConsole;

        public void Reply(string message)
            => Context.Reply(message);

        public void Broadcast(string message)
            => Context.Broadcast(message);
    }
}
