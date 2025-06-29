using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Model;

public interface IClient
{
    public ulong Guid { get; }
    public string? Name { get; }
    public string? Team { get; }
    public string? NationCode { get; }
    public bool IsConnected { get; }
    public bool HasSentFirstUpdate { get; }
    public bool SupportsCSPCustomUpdate { get; }

    public void SendPacket<TPacket>(TPacket packet) where TPacket : IOutgoingNetworkPacket;
    public void SendPacketUdp<TPacket>(in TPacket packet) where TPacket : IOutgoingNetworkPacket;
}

public interface IConnectableClient : IClient
{
    
}
