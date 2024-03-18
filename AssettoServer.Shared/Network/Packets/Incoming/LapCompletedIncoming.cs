namespace AssettoServer.Shared.Network.Packets.Incoming;

public struct LapCompletedIncoming : IIncomingNetworkPacket
{
    public uint Timestamp;
    public uint LapTime;
    public byte SplitCount;
    public uint[] Splits;
    public byte Cuts;
    public byte NumLap;

    public void FromReader(PacketReader reader)
    {
        Timestamp = reader.Read<uint>();
        LapTime = reader.Read<uint>();
        SplitCount = reader.Read<byte>();
        Splits = new uint[SplitCount];
        for (int i = 0; i < SplitCount; i++)
        {
            Splits[i] = reader.Read<uint>();
        }

        Cuts = reader.Read<byte>();
        NumLap = reader.Read<byte>();
    }
}
