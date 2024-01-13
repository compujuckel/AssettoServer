using System.Collections.Generic;

namespace AssettoServer.Server.Configuration.Serialization;

public class ConfigurationObject
{
    public required List<ConfigurationProperty> Properties { get; init; }
}
