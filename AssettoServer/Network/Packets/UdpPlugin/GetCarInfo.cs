using AssettoServer.Network.Packets.Incoming;

namespace AssettoServer.Network.Packets.UdpPlugin;

public struct GetCarInfo : IIncomingNetworkPacket
{
    public byte SessionId;

    public void FromReader(PacketReader reader)
    {
        SessionId = reader.Read<byte>();
    }
}
