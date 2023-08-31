namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class CarConnected : IOutgoingNetworkPacket
{
    public byte SessionId;
    public string? Name;
    public string? Nation;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.CarConnected);
        writer.Write(SessionId);
        writer.WriteUTF8String(Name);
        writer.WriteUTF8String(Nation);
    }
}
