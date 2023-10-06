using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Network.Packets.Shared;

public struct CSPClientMessage : IOutgoingNetworkPacket, IIncomingNetworkPacket
{
    public bool Udp;
    public byte SessionId;
    public CSPClientMessageType Type;
    public byte? TargetSessionId;
    public uint? LuaType;
    public byte[] Data;
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.Extended);
        writer.Write(Udp ? (byte)CSPMessageTypeUdp.ClientMessage : (byte)CSPMessageTypeTcp.ClientMessage);
        writer.Write(SessionId);
        writer.Write((ushort)Type);
        if (TargetSessionId.HasValue)
            writer.Write(TargetSessionId.Value);
        if (LuaType.HasValue)
            writer.Write(LuaType.Value);
        writer.WriteBytes(Data);
    }

    public void FromReader(PacketReader reader)
    {
        Data = new byte[reader.Buffer.Length - reader.ReadPosition];
        reader.ReadBytes(Data);
    }
}

public enum CSPClientMessageType : ushort
{
    HandshakeIn = 0,
    HandshakeOut = 1,
    SignatureIn = 2,
    SignatureOut = 3,
    ConditionsV1 = 1000,
    ConditionsV2 = 1001,
    ChatSharedSetup = 2000,
    TrackSharedTrigger = 10000,
    TrackSharedTriggerAllow = 10001,
    DriverNameChange = 14000,
    CarColorChange = 15000,
    CarTyreState = 15001,
    CarPartsState = 15002,
    NewModeChaseAnnouncement = 30000,
    NewModeChaseCapture = 30001,
    AdminPenalty = 50000,
    LuaMessage = 60000,
    LuaMessageTargeted = 60001,
    LuaMessageRanged = 60002,
    LuaMessageRangedTargeted = 60003,
}
