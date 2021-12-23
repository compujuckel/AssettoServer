using AssettoServer.Network.Packets;
using AssettoServer.Network.Packets.Outgoing;

namespace RaceChallengePlugin;

public class RaceChallengeUpdate : IOutgoingNetworkPacket
{
    public RaceChallengeEvent EventType { get; set; }
    public int EventData { get; set; }
    public float OwnHealth { get; set; }
    public float RivalHealth { get; set; }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write<byte>(0xAB);
        writer.Write<byte>(0x03);
        writer.Write<byte>(255);
        writer.Write<ushort>(60000);
        writer.Write(0x64C44F54);
        writer.Write(EventData);
        writer.Write(OwnHealth);
        writer.Write(RivalHealth);
        writer.Write((byte)EventType);
    }
}

public enum RaceChallengeEvent
{
    None = 0,
    RaceChallenge = 1,
    RaceCountdown = 2,
    RaceInProgress = 3,
    RaceEnded = 4
}