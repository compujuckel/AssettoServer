namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class RaceOver : IOutgoingNetworkPacket
{
    public byte SessionId;
    public uint BestResult;
    public ushort LapCounter;
    public bool PickupMode;
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.RaceOver);
        writer.Write(SessionId);
        // writer.Write(PickupMode); // Ghidra and IDA show it in different places
        writer.Write(BestResult);
        writer.Write(LapCounter);
        writer.Write(PickupMode);
    }
}
