using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing
{
    public class BallastUpdate : IOutgoingNetworkPacket
    {
        public byte SessionId;
        public float BallastKg;
        public float Restrictor;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write<byte>(0x70);
            writer.Write<byte>(1);
            writer.Write(SessionId);
            writer.Write(BallastKg);
            writer.Write(Restrictor);
        }
    }
}
