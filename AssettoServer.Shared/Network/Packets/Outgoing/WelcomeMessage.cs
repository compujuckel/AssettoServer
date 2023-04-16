namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class WelcomeMessage : IOutgoingNetworkPacket
{
    public string? Message;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.WelcomeMessage);
        writer.Write<byte>(0);
        writer.WriteUTF32String(Message, true);
    }
}
