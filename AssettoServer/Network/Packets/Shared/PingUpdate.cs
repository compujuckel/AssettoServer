using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.Shared
{
    public readonly struct PingUpdate : IOutgoingNetworkPacket
    {
        public readonly uint Time;
        public readonly ushort CurrentPing;

        public PingUpdate(uint time, ushort currentPing)
        {
            Time = time;
            CurrentPing = currentPing;
        }

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)ACServerProtocol.PingUpdate);
            writer.Write(Time);
            writer.Write(CurrentPing);
        }
    }
}
