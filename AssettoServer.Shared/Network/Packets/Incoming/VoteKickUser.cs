namespace AssettoServer.Shared.Network.Packets.Incoming;

public struct VoteKickUser : IIncomingNetworkPacket
{
    public bool Vote { get; set; }
    public byte TargetSessionId { get; set; }

    public void FromReader(PacketReader reader)
    {
        TargetSessionId = reader.Read<byte>();
        Vote = reader.Read<bool>();
    }
}
