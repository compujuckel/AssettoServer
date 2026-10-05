using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AssettoServer.Server.Configuration.Serialization;

// TODO use this instead of JsonPropertyName once https://github.com/dotnet/runtime/pull/135238 is merged
// [JsonNamingPolicy(JsonKnownNamingPolicy.PascalCase)]
public class ConfigurationObject
{
    [JsonPropertyName("Properties")]
    public required List<ConfigurationProperty> Properties { get; init; }
}
