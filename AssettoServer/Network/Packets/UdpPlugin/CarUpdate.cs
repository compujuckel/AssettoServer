using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.UdpPlugin
{
    public readonly record struct CarUpdate : IOutgoingNetworkPacket
    {
        public byte SessionId { get; init; }
        public Vector3 Position { get; init; }
        public Vector3 Velocity { get; init; }
        public byte Gear { get; init; }
        public ushort EngineRpm { get; init; }
        public float NormalizedSplinePosition { get; init; }

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)UdpPluginProtocol.CarUpdate);
            writer.Write(SessionId);
            writer.Write(Position);
            writer.Write(Velocity);
            writer.Write(Gear);
            writer.Write(EngineRpm);
            writer.Write(NormalizedSplinePosition);
        }
    }
}
