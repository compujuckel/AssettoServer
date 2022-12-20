namespace AssettoServer.Network.Packets.Incoming;

public struct TyreCompoundChangeRequest : IIncomingNetworkPacket
{
    public string CompoundName;

    public void FromReader(PacketReader reader)
    {
        CompoundName = reader.ReadASCIIString();
    }
}
