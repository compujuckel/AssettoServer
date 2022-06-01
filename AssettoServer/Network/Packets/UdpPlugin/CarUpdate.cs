using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.UdpPlugin
{
    public class CarUpdate : IOutgoingNetworkPacket
    {
        public byte SessionId;
        public Vector3 Position;
        public Vector3 Velocity;
        public byte Gear;
        public ushort EngineRpm;
        public float NormalizedSplinePosition;

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
