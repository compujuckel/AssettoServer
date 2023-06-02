using Newtonsoft.Json;

namespace AssettoServer.Shared.Network.Http.Responses;

public class InfoResponse
{
    public IEnumerable<string>? Cars { get; set; }
    public int Clients { get; set; }
    public IEnumerable<string>? Country { get; set; }
    [JsonProperty("cport")]
    public int CPort { get; set; }
    public IEnumerable<uint>? Durations { get; set; }
    public bool Extra { get; set; }
    public int Inverted { get; set; }
    public string Ip { get; set; } = "";
    public string? Json { get; set; } = null;
    public bool L { get; set; } = false;

    [JsonProperty("maxclients")]
    public int MaxClients { get; set; }
    public string? Name { get; set; }
    public bool Pass { get; set; }
    public bool Pickup { get; set; }
    public bool Pit { get; set; }
    public ushort Port { get; set; }
    public int Session { get; set; }
    [JsonProperty("sessiontypes")]
    public IEnumerable<int>? SessionTypes { get; set; }
    public bool Timed { get; set; }
    [JsonProperty("timeleft")]
    public int TimeLeft { get; set; }
    [JsonProperty("timeofday")]
    public int TimeOfDay { get; set; }
    public long Timestamp { get; set; }
    [JsonProperty("tport")]
    public ushort TPort { get; set; }
    public string? Track { get; set; }
    public string? PoweredBy { get; set; }
}
