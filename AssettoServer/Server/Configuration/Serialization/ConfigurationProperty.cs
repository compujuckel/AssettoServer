using System.Text.Json.Serialization;

namespace AssettoServer.Server.Configuration.Serialization;

// TODO use this instead of JsonPropertyName once https://github.com/dotnet/runtime/pull/135238 is merged
// [JsonNamingPolicy(JsonKnownNamingPolicy.PascalCase)]
public class ConfigurationProperty
{
    [JsonPropertyName("Name")]
    public required string Name { get; init; }
    [JsonPropertyName("Value")]
    public object? Value { get; init; }
    [JsonPropertyName("Type")]
    public required string Type { get; init; }
    [JsonPropertyName("ReadOnly")]
    public bool ReadOnly { get; init; }
    [JsonPropertyName("Description")]
    public string? Description { get; init; }
    [JsonPropertyName("Nullable")]
    public bool Nullable { get; init; }
    [JsonPropertyName("EntryType")]
    public string? EntryType { get; init; }
    [JsonPropertyName("ValidValues")]
    public string []? ValidValues { get; init; }
}
