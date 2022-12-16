namespace AssettoServer.Network.Packets.Incoming;

public struct SpectateCar : IIncomingNetworkPacket
{
    public byte SessionId;
    public byte CameraMode;

    public void FromReader(ref PacketReader reader)
    {
        SessionId = reader.Read<byte>();
        CameraMode = reader.Read<byte>();
    }
}
