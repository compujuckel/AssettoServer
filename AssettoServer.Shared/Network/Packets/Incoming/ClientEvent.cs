using System.Buffers;
using System.Numerics;

namespace AssettoServer.Shared.Network.Packets.Incoming;

public struct ClientEvent : IIncomingNetworkPacket, IDisposable
{
    public ArraySegment<SingleClientEvent> ClientEvents;

    public readonly record struct SingleClientEvent(ClientEventType Type, byte TargetSessionId, float Speed, Vector3 Position, Vector3 RelPosition);

    public void FromReader(PacketReader reader)
    {
        var count = reader.Read<short>();
        var array = ArrayPool<SingleClientEvent>.Shared.Rent(count);
        ClientEvents = new ArraySegment<SingleClientEvent>(array, 0, count);

        for (int i = 0; i < count; i++)
        {
            var type = (ClientEventType)reader.Read<byte>();

            array[i] = new SingleClientEvent
            {
                Type = type,
                TargetSessionId = type == ClientEventType.CollisionWithCar ? reader.Read<byte>() : (byte)0,
                Speed = type == ClientEventType.JumpStartPenalty ? 0 : reader.Read<float>(),
                Position = type == ClientEventType.JumpStartPenalty ? Vector3.Zero : reader.Read<Vector3>(),
                RelPosition = type == ClientEventType.JumpStartPenalty ? Vector3.Zero : reader.Read<Vector3>()
            };
        }
    }

    public void Dispose()
    {
        ArrayPool<SingleClientEvent>.Shared.Return(ClientEvents.Array!);
    }
}
