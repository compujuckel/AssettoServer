using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.UdpPlugin
{
    public class ClientEvent : IOutgoingNetworkPacket
    {
        public byte EventType;
        public byte SessionId;
        public byte? TargetSessionId;
        public float Speed;
        public Vector3 WorldPosition;
        public Vector3 RelPosition;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)UdpPluginProtocol.ClientEvent);
            writer.Write(EventType);
            writer.Write(SessionId);
            if (EventType == (byte)ClientEventType.CollisionWithCar)
            {
                if (TargetSessionId == null)
                    throw new ArgumentException("ClientEvent PlayerCollision had TargetSessionId null");
                writer.Write(TargetSessionId.Value);
            }
            writer.Write(Speed);
            writer.Write(WorldPosition);
            writer.Write(RelPosition);
        }
    }
}
