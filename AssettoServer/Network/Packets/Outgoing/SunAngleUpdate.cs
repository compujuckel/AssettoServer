using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing
{
    public class SunAngleUpdate : IOutgoingNetworkPacket
    {
        public float SunAngle;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)ACServerProtocol.SunAngleUpdate);
            writer.Write(SunAngle);
        }
    }
}
