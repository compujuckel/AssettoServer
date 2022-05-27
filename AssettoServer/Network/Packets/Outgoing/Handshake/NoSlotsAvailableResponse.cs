using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing.Handshake
{
    public readonly struct NoSlotsAvailableResponse : IOutgoingNetworkPacket
    {
        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)ACServerProtocol.NoSlotsAvailable);
        }
    }
}
