namespace AssettoServer.Network.Packets.Incoming;

public struct P2PUpdateRequest : IIncomingNetworkPacket
{
    public short P2PCount;
    public bool Active;

    public void FromReader(PacketReader reader)
    {
        P2PCount = reader.Read<short>();
        Active = reader.Read<bool>();
    }
}
