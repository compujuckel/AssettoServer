using AssettoServer.Shared.Network.Packets.Incoming;

namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class WelcomeMessage : IOutgoingNetworkPacket, IIncomingNetworkPacket
{
    public string? Message;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.WelcomeMessage);
        writer.Write<byte>(0);
        writer.WriteUTF32String(Message, true);
    }

    public void FromReader(PacketReader reader)
    {
        reader.Read<byte>();
        Message = reader.ReadUTF32String(true);
    }
}
