using System.Text.Json.Serialization;

namespace AssettoServer.Server.Configuration;

public class CMWrapperParams
{
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
