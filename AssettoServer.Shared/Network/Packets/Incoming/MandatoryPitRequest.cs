namespace AssettoServer.Shared.Network.Packets.Incoming;

public struct MandatoryPitRequest : IIncomingNetworkPacket
{
    public bool MandatoryPit;

    public void FromReader(PacketReader reader)
    {
        MandatoryPit = reader.Read<bool>();
    }
}
