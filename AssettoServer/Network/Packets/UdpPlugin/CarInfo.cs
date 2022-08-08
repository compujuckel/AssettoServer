using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.UdpPlugin;

public readonly record struct CarInfo : IOutgoingNetworkPacket
{
    public byte CarId { get; init; }
    public bool IsConnected { get; init; }
    public string? Model { get; init; }
    public string? Skin { get; init; }
    public string? DriverName { get; init; }
    public string? DriverTeam { get; init; }
    public string? DriverGuid { get; init; }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)UdpPluginProtocol.CarInfo);
        writer.Write(CarId);
        writer.Write(IsConnected);
        writer.WriteUTF32String(Model);
        writer.WriteUTF32String(Skin);
        writer.WriteUTF32String(DriverName);
        writer.WriteUTF32String(DriverTeam);
        writer.WriteUTF32String(DriverGuid);
    }
}
