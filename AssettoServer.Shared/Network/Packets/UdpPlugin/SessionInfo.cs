using System.Text;
using AssettoServer.Shared.Model;
using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Network.Packets.UdpPlugin;

public readonly record struct SessionInfo : IOutgoingNetworkPacket
{
    public bool IsNew { get; init; }
    public byte ProtocolVersion { get; init; }
    public byte SessionIndex { get; init; }
    public byte CurrentSessionIndex { get; init; }
    public byte SessionCount { get; init; }
    public string? ServerName { get; init; }
    public string? Track { get; init; }
    public string? TrackConfig { get; init; }
    public string? Name { get; init; }
    public SessionType SessionType { get; init; }
    public ushort SessionTime { get; init; }
    public ushort SessionLaps { get; init; }
    public ushort SessionWaitTime { get; init; }
    public byte AmbientTemperature { get; init; }
    public byte RoadTemperature { get; init; }
    public string? WeatherGraphics { get; init; }
    public int ElapsedMs { get; init; }  // ms to race start (might be negative if server has wait time)

    public void ToWriter(ref PacketWriter writer)
    {
        if (IsNew)
            writer.Write((byte)UdpPluginProtocol.NewSession);
        else
            writer.Write((byte)UdpPluginProtocol.SessionInfo);
        writer.Write(ProtocolVersion);
        writer.Write(SessionIndex);
        writer.Write(CurrentSessionIndex);
        writer.Write(SessionCount);
        writer.WriteUTF32String(ServerName);
        writer.WriteString(Track, Encoding.UTF8);
        writer.WriteString(TrackConfig, Encoding.UTF8);
        writer.WriteString(Name, Encoding.UTF8);
        writer.Write(SessionType);
        writer.Write(SessionTime);
        writer.Write(SessionLaps);
        writer.Write(SessionWaitTime);
        writer.Write(AmbientTemperature);
        writer.Write(RoadTemperature);
        writer.WriteUTF32String(WeatherGraphics);
        writer.Write(ElapsedMs);
    }
}
