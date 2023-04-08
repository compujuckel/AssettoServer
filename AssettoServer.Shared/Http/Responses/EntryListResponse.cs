using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AssettoServer.Shared.Http.Responses;

[JsonObject(NamingStrategyType = typeof(DefaultNamingStrategy))]
public class EntryListResponseCar
{
    public string? Model { get; set; }
    public string? Skin { get; set; }
    public string? DriverName { get; set; }
    public string? DriverTeam { get; set; }
    public bool IsRequestedGUID { get; set; }
    public bool IsEntryList { get; set; }
    public bool IsConnected { get; set; }
}

[JsonObject(NamingStrategyType = typeof(DefaultNamingStrategy))]
public class EntryListResponse
{
    public IEnumerable<string>? Features { get; set; }
    public IEnumerable<EntryListResponseCar>? Cars { get; set; }
}
