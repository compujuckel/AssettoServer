namespace AssettoServer.Network.Packets.Outgoing;

public interface IOutgoingNetworkPacket
{
    void ToWriter(ref PacketWriter writer);
}
