using AssettoServer.Network.Packets;
using AssettoServer.Network.Packets.Outgoing;

namespace RaceChallengePlugin;

public class RaceChallengeStatus : IOutgoingNetworkPacket
{
    public RaceChallengeEvent EventType { get; set; }
    public int EventData { get; set; }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write<byte>(0xAB);
        writer.Write<byte>(0x03);
        writer.Write<byte>(255);
        writer.Write<ushort>(60000);
        writer.Write(0x98D1F7A9);
        writer.Write(EventData);
        writer.Write((byte)EventType);
    }
}

public enum RaceChallengeEvent
{
    None = 0,
    RaceChallenge = 1,
    RaceCountdown = 2,
    RaceEnded = 3
}
