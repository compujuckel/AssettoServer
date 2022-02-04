using System;

namespace AssettoServer.Network.Packets.Outgoing;

public readonly struct BatchedPositionUpdate : IOutgoingNetworkPacket
{
    public readonly uint Timestamp;
    public readonly ushort Ping;
    public readonly ArraySegment<PositionUpdateOut> Updates;

    public BatchedPositionUpdate(uint timestamp, ushort ping, ArraySegment<PositionUpdateOut> updates)
    {
        Timestamp = timestamp;
        Ping = ping;
        Updates = updates;
    }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write<byte>(0x48);
        writer.Write(Timestamp);
        writer.Write(Ping);
        writer.Write((byte)Updates.Count);
        for (int i = 0; i < Updates.Count; i++)
        {
            Updates[i].ToWriter(ref writer, true);
        }
    }
}