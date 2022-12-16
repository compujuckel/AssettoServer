using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.Shared;

public struct CSPClientMessage : IOutgoingNetworkPacket, IIncomingNetworkPacket
{
    public byte SessionId;
    public CSPClientMessageType Type;
    public uint? LuaType;
    public byte[] Data;
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.Extended);
        writer.Write((byte)CSPMessageTypeTcp.ClientMessage);
        writer.Write(SessionId);
        writer.Write((ushort)Type);
        if(LuaType.HasValue)
            writer.Write(LuaType.Value);
        writer.WriteBytes(Data);
    }

    public void FromReader(ref PacketReader reader)
    {
        Data = new byte[reader.Buffer.Length - reader.ReadPosition];
        reader.ReadBytes(Data);
    }
}

public enum CSPClientMessageType
{
    HandshakeIn = 0,
    HandshakeOut = 1,
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
    LuaMessage = 60000
}
