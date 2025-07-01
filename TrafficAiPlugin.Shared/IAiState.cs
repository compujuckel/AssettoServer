using AssettoServer.Shared.Model;

namespace TrafficAiPlugin.Shared;

public interface IAiState
{
    public CarStatus Status { get; }
    public byte SessionId { get; }
}
