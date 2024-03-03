namespace AssettoServer.Shared.Network.Packets.Incoming;

public struct VoteNextSession : IIncomingNetworkPacket
{
    public bool Vote { get; set; }

    public void FromReader(PacketReader reader)
    {
        Vote = reader.Read<bool>();
    }
}
