using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing.Handshake
{
    public readonly struct UnsupportedProtocolResponse : IOutgoingNetworkPacket
    {
        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write<byte>(0x42);
        }
    }
}
