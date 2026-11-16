using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AssettoServer.Server.Configuration.Serialization;

[JsonNamingPolicy(JsonKnownNamingPolicy.PascalCase)]
public class ConfigurationObject
{
    public required List<ConfigurationProperty> Properties { get; init; }
}
