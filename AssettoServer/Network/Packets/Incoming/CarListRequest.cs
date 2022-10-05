namespace AssettoServer.Network.Packets.Incoming;

public struct CarListRequest : IIncomingNetworkPacket
{
    public int PageIndex;

    public void FromReader(PacketReader reader)
    {
        PageIndex = reader.Read<byte>();
    }
}
