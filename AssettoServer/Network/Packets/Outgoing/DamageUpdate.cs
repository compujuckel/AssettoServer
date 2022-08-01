using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing
{
    public class DamageUpdate : IOutgoingNetworkPacket
    {
        public byte SessionId;
        public float[] DamageZoneLevel;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)ACServerProtocol.DamageUpdate);
            writer.Write(SessionId);

            for (int i = 0; i < 5; i++)
                writer.Write(DamageZoneLevel[i]);
        }
    }
}
