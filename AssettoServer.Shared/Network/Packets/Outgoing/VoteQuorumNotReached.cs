namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class VoteQuorumNotReached : IOutgoingNetworkPacket
{
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.VoteQuorumNotReached);
        writer.Write(0u);
    }
}
