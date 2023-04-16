namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class TyreCompoundUpdate : IOutgoingNetworkPacket
{
    public string? CompoundName;
    public byte SessionId;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.TyreCompoundChange);
        writer.Write(SessionId);
        writer.WriteASCIIString(CompoundName);
    }
}
