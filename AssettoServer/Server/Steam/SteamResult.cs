namespace AssettoServer.Server.Steam;

public class SteamResult
{
    public bool Success { get; init; }
    public ulong SteamId { get; init; }
    public ulong OwnerSteamId { get; init; }
    public string? ErrorReason { get; init; }
}
