using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Incoming
{
    public struct DamageUpdateIncoming : IIncomingNetworkPacket
    {
        public float[] DamageZoneLevel;

        public void FromReader(PacketReader reader)
        {
            DamageZoneLevel = new float[5];
            for (int i = 0; i < 5; i++)
            {
                DamageZoneLevel[i] = reader.Read<float>();
            }
        }
    }
}
