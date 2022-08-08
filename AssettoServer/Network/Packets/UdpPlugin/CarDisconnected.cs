using System.Text;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.UdpPlugin;

public readonly record struct CarDisconnected : IOutgoingNetworkPacket
{
    public string? DriverName { get; init; }
    public string? DriverGuid { get; init; }
    public byte SessionId { get; init; }
    public string? CarModel { get; init; }
    public string? CarSkin { get; init; }

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)UdpPluginProtocol.ClosedConnection);
        writer.WriteUTF32String(DriverName);
        writer.WriteUTF32String(DriverGuid);
        writer.Write(SessionId);
        writer.WriteString(CarModel, Encoding.UTF8);
        writer.WriteString(CarSkin, Encoding.UTF8);
    }
}
