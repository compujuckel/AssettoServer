namespace ReplayPlugin;

public class ReplayPlayerInfo
{
    public required string Name { get; init; }
    public required string Guid { get; init; }
    public string? OwnerGuid { get; init; }
    public string? NationCode { get; init; }
}
