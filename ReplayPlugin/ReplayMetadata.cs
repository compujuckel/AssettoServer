namespace ReplayPlugin;

public class ReplayMetadata
{
    public required string ServerName { get; init; }
    public required string ServerAddress { get; init; }
    public required string Timestamp { get; init; }
    public required Dictionary<uint, Dictionary<byte, ReplayPlayerInfo>> PlayerInfos { get; init; }
}
