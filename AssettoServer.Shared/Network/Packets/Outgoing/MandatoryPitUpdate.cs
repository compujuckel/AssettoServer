namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class MandatoryPitUpdate : IOutgoingNetworkPacket
{
    public byte SessionId;
    public bool MandatoryPit;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.MandatoryPitUpdate);
        writer.Write(SessionId);
        writer.Write(MandatoryPit);
    }
}
