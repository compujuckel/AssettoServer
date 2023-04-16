using Newtonsoft.Json;

namespace AssettoServer.Shared.Network.Http.Responses;

public class CMContentConfiguration
{
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, CMContentEntryCar>? Cars { get; set; }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public CMContentEntryVersionized? Track { get; set; }
}

public class CMContentEntryCar : CMContentEntryVersionized
{
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, CMContentEntry>? Skins { get; set; }
}

public class CMContentEntryVersionized : CMContentEntry
{
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Version { get; set; }
}

public class CMContentEntry
{
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Url { get; set; }
}
