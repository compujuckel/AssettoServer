using AssettoServer.Network.Packets;
using AssettoServer.Network.Packets.Outgoing;

namespace RaceChallengePlugin;

public class RaceChallengeUpdate : IOutgoingNetworkPacket
{
    public float OwnHealth { get; set; }
    public float OwnRate { get; set; }
    public float RivalHealth { get; set; }
    public float RivalRate { get; set; }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write<byte>(0xAB);
        writer.Write<byte>(0x03);
        writer.Write<byte>(255);
        writer.Write<ushort>(60000);
        writer.Write(0xC069E2E7);
        writer.Write(OwnHealth);
        writer.Write(OwnRate);
        writer.Write(RivalHealth);
        writer.Write(RivalRate);
    }
}
