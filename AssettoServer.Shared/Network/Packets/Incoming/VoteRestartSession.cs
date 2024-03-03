namespace AssettoServer.Shared.Network.Packets.Incoming;

public struct VoteRestartSession : IIncomingNetworkPacket
{
    public bool Vote { get; set; }

    public void FromReader(PacketReader reader)
    {
        Vote = reader.Read<bool>();
    }
}
