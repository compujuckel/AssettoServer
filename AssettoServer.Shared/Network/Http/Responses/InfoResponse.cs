using System.Text.Json.Serialization;

namespace AssettoServer.Shared.Network.Http.Responses;

public class InfoResponse
{
    [JsonPropertyName("cars")]
    public IEnumerable<string>? Cars { get; set; }
    [JsonPropertyName("clients")]
    public int Clients { get; set; }
    [JsonPropertyName("country")]
    public IEnumerable<string>? Country { get; set; }
    [JsonPropertyName("cport")]
    public int CPort { get; set; }
    [JsonPropertyName("durations")]
    public IEnumerable<int>? Durations { get; set; }
    [JsonPropertyName("extra")]
    public bool Extra { get; set; }
    [JsonPropertyName("inverted")]
    public int Inverted { get; set; }
    [JsonPropertyName("ip")]
    public string Ip { get; set; } = "";
    [JsonPropertyName("json")]
    public string? Json { get; set; } = null;
    [JsonPropertyName("l")]
    public bool L { get; set; } = false;
    [JsonPropertyName("maxclients")]
    public int MaxClients { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("pass")]
    public bool Pass { get; set; }
    [JsonPropertyName("pickup")]
    public bool Pickup { get; set; }
    [JsonPropertyName("pit")]
    public bool Pit { get; set; }
    [JsonPropertyName("port")]
    public ushort Port { get; set; }
    [JsonPropertyName("session")]
    public int Session { get; set; }
    [JsonPropertyName("sessiontypes")]
    public IEnumerable<int>? SessionTypes { get; set; }
    [JsonPropertyName("timed")]
    public bool Timed { get; set; }
    [JsonPropertyName("timeleft")]
    public int TimeLeft { get; set; }
    [JsonPropertyName("timeofday")]
    public int TimeOfDay { get; set; }
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
    [JsonPropertyName("tport")]
    public ushort TPort { get; set; }
    [JsonPropertyName("track")]
    public string? Track { get; set; }
    [JsonPropertyName("poweredBy")]
    public string? PoweredBy { get; set; }
}
