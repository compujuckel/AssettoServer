namespace AssettoServer.Shared.Model;

public interface IEntryCar<out TClient> where TClient : IClient
{
    public byte SessionId { get; }
    public bool IsSpectator { get; }
    public string Model { get; }
    public string Skin { get; }
    public CarStatus Status { get; }
    public TClient? Client { get; }
    public bool AiControlled { get; }
    public string? AiName { get; }
}
