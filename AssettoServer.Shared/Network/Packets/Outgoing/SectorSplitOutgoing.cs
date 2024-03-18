namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class SectorSplitOutgoing : IOutgoingNetworkPacket
{
    public byte SessionId;
    public byte SplitIndex;
    public uint SplitTime;
    public byte Cuts;
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.SectorSplit);
        writer.Write(SessionId);
        writer.Write(SplitIndex);
        writer.Write(SplitTime);
        writer.Write(Cuts);
    }
}
