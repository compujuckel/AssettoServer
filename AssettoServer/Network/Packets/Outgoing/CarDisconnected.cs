using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing
{
    public class CarDisconnected : IOutgoingNetworkPacket
    {
        public byte SessionId;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)ACServerProtocol.CarDisconnected);
            writer.Write(SessionId);
        }
    }
}
