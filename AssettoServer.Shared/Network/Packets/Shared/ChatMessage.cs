using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Network.Packets.Shared;

public struct ChatMessage : IIncomingNetworkPacket, IOutgoingNetworkPacket
{
    public byte SessionId;
    public string Message;

    public void FromReader(PacketReader reader)
    {
        SessionId = reader.Read<byte>();
        Message = reader.ReadUTF32String();
    }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.Chat);
        writer.Write(SessionId);
        writer.WriteUTF32String(Message);
    }
}
