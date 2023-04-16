namespace AssettoServer.Shared.Network.Packets.Outgoing.Handshake;

public readonly struct SessionClosedResponse : IOutgoingNetworkPacket
{
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.SessionClosed);
    }
}
