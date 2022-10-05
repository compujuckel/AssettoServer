namespace AssettoServer.Network.Packets.Outgoing;

public class CarDisconnected : IOutgoingNetworkPacket
{
    public byte SessionId;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.CarDisconnected);
        writer.Write(SessionId);
    }
}
