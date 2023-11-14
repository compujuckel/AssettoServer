namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class BatchedPacket : IOutgoingNetworkPacket
{
    public List<IOutgoingNetworkPacket> Packets { get; } = [];
    
    public void ToWriter(ref PacketWriter writer)
    {
        throw new InvalidOperationException("BatchedPacket can only be sent via TCP");
    }
}
