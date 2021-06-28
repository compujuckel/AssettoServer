using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing
{
    public class WelcomeMessage : IOutgoingNetworkPacket
    {
        public string Message;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write<byte>(0x51);
            writer.Write<byte>(0);
            writer.WriteUTF32String(Message, true);
        }
    }
}
