using System.Drawing;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing;
using Serilog;

namespace AssettoServer.Shared.Network.Packets.Shared;

public class CSPAdminPenalty : IIncomingNetworkPacket, IOutgoingNetworkPacket
{
    public byte SessionId;
    public byte[]? Message;

    public void FromReader(PacketReader reader)
    {
        Message = new byte[80];
        reader.ReadBytes(Message);
    }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.Extended);
        writer.Write((byte)CSPMessageTypeTcp.ClientMessage);
        writer.Write(SessionId);
        writer.Write((ushort)CSPClientMessageType.AdminPenalty);
        writer.WriteBytes(Message);
    }
}
