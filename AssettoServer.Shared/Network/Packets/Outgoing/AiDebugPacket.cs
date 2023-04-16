namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class AiDebugPacket : IOutgoingNetworkPacket
{
    public const int Length = 20;

    public byte[] SessionIds { get; init; } = new byte[Length];
    public byte[] CurrentSpeeds { get; init; } = new byte[Length];
    public byte[] TargetSpeeds { get; init;  } = new byte[Length];
    public byte[] MaxSpeeds { get; init;  } = new byte[Length];
    public short[] ClosestAiObstacles { get; init; } = new short[Length];
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.Extended);
        writer.Write((byte)CSPMessageTypeTcp.ClientMessage);
        writer.Write<byte>(255);
        writer.Write<ushort>(60000);
        writer.Write(0xB8DC08B3);
        for (int i = 0; i < Length; i++)
        {
            writer.Write(ClosestAiObstacles[i]);
        }
        writer.WriteBytes(CurrentSpeeds);
        writer.WriteBytes(MaxSpeeds);
        writer.WriteBytes(SessionIds);
        writer.WriteBytes(TargetSpeeds);
    }
}
