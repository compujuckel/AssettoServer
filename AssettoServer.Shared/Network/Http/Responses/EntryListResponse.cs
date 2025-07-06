using System.Text.Json.Serialization;

namespace AssettoServer.Shared.Network.Http.Responses;

public class EntryListResponseCar
{
    [JsonPropertyName("Model")]
    public required string Model { get; set; }
    [JsonPropertyName("Skin")]
    public string? Skin { get; set; }
    [JsonPropertyName("DriverName")]
    public string? DriverName { get; set; }
    [JsonPropertyName("DriverTeam")]
    public string? DriverTeam { get; set; }
    [JsonPropertyName("IsRequestedGUID")]
    public bool IsRequestedGUID { get; set; }
    [JsonPropertyName("IsEntryList")]
    public bool IsEntryList { get; set; }
    [JsonPropertyName("IsConnected")]
    public bool IsConnected { get; set; }
}

public class EntryListResponse
{
    [JsonPropertyName("Features")]
    public IEnumerable<string>? Features { get; set; }
    [JsonPropertyName("Cars")]
    public IEnumerable<EntryListResponseCar>? Cars { get; set; }
}
