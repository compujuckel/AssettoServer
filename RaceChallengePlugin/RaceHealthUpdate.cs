namespace AssettoServer.Network.Packets.Outgoing;

public class RaceHealthUpdate : IOutgoingNetworkPacket
{
    public float OwnHealth { get; set; }
    public float RivalHealth { get; set; }
    public byte RivalId { get; set; }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write<byte>(0xAB);
        writer.Write<byte>(0x03);
        writer.Write<byte>(255);
        writer.Write<ushort>(60000);
        //writer.Write(0xFBCF0841);
        writer.Write(0x4108CFFB);
        writer.Write(OwnHealth);
        writer.Write(RivalHealth);
        writer.Write(RivalId);
    }
}