using System.Text.Json.Serialization;

namespace AssettoServer.Server.Configuration;

public class CMWrapperParams
{
    [JsonPropertyName("description")]
    public string? Description { get; init; }
    
    // CM Direct Share Bandwidth limit in Bytes/second
    [JsonPropertyName("downloadSpeedLimit")]
    public long DownloadSpeedLimit { get; init; } = 0;
    
    [JsonPropertyName("downloadPasswordOnly")]
    public bool DownloadPasswordOnly { get; set; }
}
