using AssettoServer.Network.Packets;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GroupStreetRacingPlugin.Packets
{
    public class GroupStreetRacingHazardsPacket : IOutgoingNetworkPacket
    {
        public const int Length = 20;
        public byte[] SessionIds { get; init; } = new byte[Length];
        public byte[] HealthOfCars { get; init; } = new byte[Length];

        public GroupStreetRacingHazardsPacket(byte[] sessionIds, byte[] healthOfCars)
        {
            SessionIds = sessionIds;
            HealthOfCars = healthOfCars;
        }

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)ACServerProtocol.Extended);
            writer.Write((byte)CSPMessageTypeTcp.ClientMessage);
            writer.Write<byte>(255);
            writer.Write<ushort>(60000);
            writer.Write(0xBAA23067);
            writer.WriteBytes(HealthOfCars);
            writer.WriteBytes(SessionIds);            
        }
    }
}
