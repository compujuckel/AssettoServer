namespace AssettoServer.Shared.Network.Packets.Outgoing;

public readonly ref struct BatchedPositionUpdate : IOutgoingNetworkPacket
{
    public readonly uint Timestamp;
    public readonly ushort Ping;
    public readonly ReadOnlySpan<PositionUpdateOut> Updates;

    public BatchedPositionUpdate(uint timestamp, ushort ping, ReadOnlySpan<PositionUpdateOut> updates)
    {
        Timestamp = timestamp;
        Ping = ping;
        Updates = updates;
    }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.MegaPacket);
        writer.Write(Timestamp);
        writer.Write(Ping);
        writer.Write((byte)Updates.Length);
        for (int i = 0; i < Updates.Length; i++)
        {
            Updates[i].ToWriter(ref writer, true);
        }
    }
}
