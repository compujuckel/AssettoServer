using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Model;

public interface IEntryCar<out TClient> where TClient : IClient
{
    public byte SessionId { get; }
    public ushort Ping { get; }
    public int TimeOffset { get; }
    public bool IsSpectator { get; }
    public string Model { get; }
    public string Skin { get; }
    public float Ballast { get; }
    public int Restrictor { get; }
    public bool EnableCollisions { get; }
    public CarStatus Status { get; }
    public TClient? Client { get; }
    public List<ulong> AllowedGuids { get; }
    public bool AiControlled { get; }
    public string? AiName { get; }
    public AiMode AiMode { get; }
    public DriverOptionsFlags DriverOptionsFlags { get; }
    public bool HasUpdateToSend { get; set; }
    public IEntryCar<IClient>? TargetCar { get; }


    public bool GetPositionUpdateForCar(IEntryCar<IClient> toCar, out PositionUpdateOut positionUpdateOut);
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
