namespace AssettoServer.Network.Packets.Incoming;

public interface IIncomingNetworkPacket
{
    void FromReader(PacketReader reader);
}
