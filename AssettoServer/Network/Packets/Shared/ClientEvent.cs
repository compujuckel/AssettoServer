using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Shared
{
    public struct ClientEvent : IIncomingNetworkPacket, IOutgoingNetworkPacket
    {
        public byte SessionId;
        public short Count;
        public byte Type;

        public void FromReader(PacketReader reader)
        {
            Count = reader.Read<short>();
            Type = reader.Read<byte>();
        }

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)0x82);
            writer.Write(Type);
            writer.Write(SessionId);
        }
    }
}
