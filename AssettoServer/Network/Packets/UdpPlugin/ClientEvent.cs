using System;
using System.Numerics;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.UdpPlugin;

public readonly record struct ClientEvent : IOutgoingNetworkPacket
{
    public byte EventType { get; init; }
    public byte SessionId { get; init; }
    public byte? TargetSessionId { get; init; }
    public float Speed { get; init; }
    public Vector3 WorldPosition { get; init; }
    public Vector3 RelPosition { get; init; }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)UdpPluginProtocol.ClientEvent);
        writer.Write(EventType);
        writer.Write(SessionId);
        if (EventType == (byte)ClientEventType.CollisionWithCar)
        {
            if (!TargetSessionId.HasValue)
                throw new ArgumentException("ClientEvent PlayerCollision had TargetSessionId null");
            writer.Write(TargetSessionId.Value);
        }
        writer.Write(Speed);
        writer.Write(WorldPosition);
        writer.Write(RelPosition);
    }
}
