using AssettoServer.Server;

namespace TrafficAiPlugin.Shared;

public interface IEntryCarTrafficAi
{
    public IAiState?[] LastSeenAiState { get; }
    public EntryCar EntryCar { get; }
    
    public void SetAiOverbooking(int count);
    public bool TryResetPosition();
    public void AiUpdate();
}
