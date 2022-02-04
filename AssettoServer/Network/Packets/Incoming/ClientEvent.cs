using System.Collections.Generic;
using System.Numerics;

namespace AssettoServer.Network.Packets.Incoming;

public struct ClientEvent : IIncomingNetworkPacket
{
    public short Count;
    public List<SingleClientEvent> ClientEvents;
        
    public class SingleClientEvent
    {
        public ClientEventType Type;
        public byte TargetSessionId;
        public float Speed;
        public Vector3 Position;
        public Vector3 RelPosition;
    }

    public enum ClientEventType
    {
        PlayerCollision = 0x0A,
        EnvironmentCollision = 0x0B
    }

    public void FromReader(PacketReader reader)
    {
        Count = reader.Read<short>();
        ClientEvents = new List<SingleClientEvent>();

        for (int i = 0; i < Count; i++)
        {
            var evt = new SingleClientEvent();
                
            evt.Type = (ClientEventType)reader.Read<byte>();

            if (evt.Type == ClientEventType.PlayerCollision)
            {
                evt.TargetSessionId = evt.Type == ClientEventType.PlayerCollision ? reader.Read<byte>() : (byte)0;
            }

            evt.Speed = reader.Read<float>();
            evt.Position = reader.Read<Vector3>();
            evt.RelPosition = reader.Read<Vector3>();
                
            ClientEvents.Add(evt);
        }
    }
}