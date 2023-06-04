using AssettoServer.Shared.Model;
using AssettoServer.Shared.Network.Packets.Incoming;

namespace AssettoServer.Shared.Network.Packets.UdpPlugin;

public struct SetSessionInfo : IIncomingNetworkPacket
{
    public byte SessionIndex;
    public string SessionName;
    public SessionType SessionType;
    public int Laps;
    public int Time;
    public uint WaitTime;

    public void FromReader(PacketReader reader)
    {
        SessionIndex = reader.Read<byte>();
        SessionName = reader.ReadUTF32String();
        SessionType = reader.Read<SessionType>();
        Laps = reader.Read<int>();
        Time = reader.Read<int>();
        WaitTime = reader.Read<uint>();
    }
}
