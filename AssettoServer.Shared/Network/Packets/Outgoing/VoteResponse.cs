using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Network.Packets.Outgoing;

public struct VoteResponse : IOutgoingNetworkPacket
{
    public byte Protocol { get; set; }
    public byte? Target { get; set; }
    public byte Quorum { get; set; }
    public byte VoteCount { get; set; }
    public uint Time { get; set; }
    public byte LastVoter { get; set; }
    public bool LastVote { get; set; }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)Protocol);
        if (Target == null)
            writer.Write((byte)0x0);
        else
            writer.Write((byte)Target);
                
        writer.Write((byte)Quorum);
        writer.Write((byte)VoteCount);
        writer.Write((uint)Time);
        
        writer.Write((byte)LastVoter);
        if (LastVote)
            writer.Write((byte)0x1);
        else
            writer.Write((byte)0x0);
    }
}
