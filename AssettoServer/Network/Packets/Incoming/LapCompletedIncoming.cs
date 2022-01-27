namespace AssettoServer.Network.Packets.Incoming;

public struct LapCompletedIncoming : IIncomingNetworkPacket
{
    public int Timestamp;
    public int LapTime;
    public byte SplitCount;
    public int[] Splits;
    public byte Cuts;
    public byte NumLap;

    public void FromReader(PacketReader reader)
    {
        Timestamp = reader.Read<int>();
        LapTime = reader.Read<int>();
        SplitCount = reader.Read<byte>();
        Splits = new int[SplitCount];
        for (int i = 0; i < SplitCount; i++)
        {
            Splits[i] = reader.Read<int>();
        }

        Cuts = reader.Read<byte>();
        NumLap = reader.Read<byte>();
    }
}
