namespace AssettoServer.Server.Configuration.Serialization;

public class ConfigurationProperty
{
    public required string Name { get; init; }
    public object? Value { get; init; }
    public required string Type { get; init; }
    public bool ReadOnly { get; init; }
    public string? Description { get; init; }
    public bool Nullable { get; init; }
    public string? EntryType { get; init; }
    public string []? ValidValues { get; init; }
}
