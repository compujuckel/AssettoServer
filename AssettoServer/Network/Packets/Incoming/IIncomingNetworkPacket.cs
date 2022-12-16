namespace AssettoServer.Network.Packets.Incoming;

public interface IIncomingNetworkPacket
{
    void FromReader(ref PacketReader reader);
}
