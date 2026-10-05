using System.Text.Json.Serialization;

namespace AssettoServer.Shared.Network.Http.Responses;

[JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
public class CMContentConfiguration
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, CMContentEntryCar>? Cars { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CMContentEntryVersionized? Track { get; set; }
}

[JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
public class CMContentEntryCar : CMContentEntryVersionized
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, CMContentEntry>? Skins { get; set; }
}

[JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
public class CMContentEntryVersionized : CMContentEntry
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; set; }
}

[JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
public class CMContentEntry
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public string? File { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Direct => File != null;
}
