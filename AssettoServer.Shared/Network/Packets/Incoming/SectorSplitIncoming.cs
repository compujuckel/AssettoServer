namespace AssettoServer.Shared.Network.Packets.Incoming;

public struct SectorSplitIncoming : IIncomingNetworkPacket
{
    public byte SplitIndex;
    public uint SplitTime;
    public byte Cuts;

    public void FromReader(PacketReader reader)
    {
        SplitIndex = reader.Read<byte>();
        SplitTime = reader.Read<uint>();
        Cuts = reader.Read<byte>();
    }
}
