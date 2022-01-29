using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AssettoServer.Network.Http.Responses;

[JsonObject(NamingStrategyType = typeof(DefaultNamingStrategy))]
public class EntryListResponseCar
{
    public string? Model { get; internal set; }
    public string? Skin { get; internal set; }
    public string? DriverName { get; set; }
    public string? DriverTeam { get; set; }
    public bool IsRequestedGuid { get; set; }
    public bool IsEntryList { get; set; }
    public bool IsConnected { get; set; }
}

[JsonObject(NamingStrategyType = typeof(DefaultNamingStrategy))]
public class EntryListResponse
{
    public IEnumerable<string>? Features { get; set; }
    public IEnumerable<EntryListResponseCar>? Cars { get; set; }
}