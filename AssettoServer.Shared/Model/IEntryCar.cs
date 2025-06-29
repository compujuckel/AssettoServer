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
    public AiMode AiMode { get; }
    public DriverOptionsFlags DriverOptionsFlags { get; }
}

public interface IConnectableEntryCar<out TClient> : IEntryCar<TClient> where TClient : IConnectableClient
{
}


[Flags]
public enum DriverOptionsFlags
{
    AllowColorChange = 0x10,
    AllowTeleporting = 0x20
}
