using System.Numerics;
using AssettoServer.Shared.Network.Packets.Outgoing;
using Serilog;

namespace AssettoServer.Shared.Model;

public interface IClient
{
    public CarStatus Status { get; }
    public byte SessionId { get; }
    public ulong Guid { get; }
    public string HashedGuid { get; }
    public string? Name { get; }
    public string? Team { get; }
    public string? NationCode { get; }
    
    public bool IsAdministrator { get; }
    public int SecurityLevel { get; }
    public bool IsConnected { get; set; }
    public ushort Ping { get; set; }
    public int TimeOffset { get; set; }
    public long LastPingTime { get; set; }
    public long LastPongTime { get; set; }
    public bool HasSentFirstUpdate { get; }
    public bool HasUdpEndpoint { get; }
    public bool IsDisconnectRequested { get; }
    public ChecksumStatus ChecksumStatus { get; }
    public int? CSPVersion { get; }
    public bool SupportsCSPCustomUpdate { get; }
    public IEntryCar EntryCar { get; }
    public IEntryCar? TargetCar { get; set; }
    
    public ILogger Logger { get; }
    

    public Task DisconnectAsync();
    public void SendFirstUpdate();
    public void SendPacket<TPacket>(TPacket packet) where TPacket : IOutgoingNetworkPacket;
    public void SendPacketUdp<TPacket>(in TPacket packet) where TPacket : IOutgoingNetworkPacket;
    public void SendTeleportCarPacket(Vector3 position, Vector3 direction, Vector3 velocity = default);
}
