using System.Text;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Network.Packets.Shared;

public class CSPAdminPenalty : IIncomingNetworkPacket, IOutgoingNetworkPacket
{
    public byte SessionId;
    public CSPAdminPenaltyMode Mode;
    public ushort CarIndex;
    public int PenaltyArgument;
    public string Message;
    public ulong Signature;

    public void FromReader(PacketReader reader)
    {
        Mode = (CSPAdminPenaltyMode) reader.Read<ushort>();
        CarIndex = reader.Read<ushort>();
        PenaltyArgument = reader.Read<int>();
        Message = reader.ReadStringFixed(Encoding.UTF8, 64);        
        Signature = reader.Read<ulong>();
    }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.Extended);
        writer.Write((byte)CSPMessageTypeTcp.ClientMessage);
        writer.Write(SessionId);
        writer.Write((ushort)CSPClientMessageType.AdminPenalty);
        
        writer.Write((ushort)Mode);
        writer.Write(CarIndex);
        writer.Write(PenaltyArgument);
        
        writer.WriteString(Message, Encoding.UTF8, 64);
        writer.Write(Signature);
    }
}

/// <summary>
/// Only <b>MandatoryPits</b> and <b>TeleportToPits</b> are known to work
/// </summary>
public enum CSPAdminPenaltyMode
{
    None = 0,
    MandatoryPits = 1,
    TeleportToPits = 2,
    SlowDown = 3,
    BlackFlag = 4,
    BlackFlagRelease = 5
}
