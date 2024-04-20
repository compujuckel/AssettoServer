using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace AssettoServer.Shared.Network.Http.Responses;

public class DetailResponse : InfoResponse
{
    [JsonPropertyName("players")]
    public required DetailResponsePlayerList Players { get; set; }
    [JsonPropertyName("until")]
    public long Until { get; set; }
    [JsonPropertyName("content")]
    public CMContentConfiguration? Content { get; set; }
    [JsonPropertyName("trackBase")]
    public string? TrackBase { get; set; }
    [JsonPropertyName("city")]
    public string? City { get; set; }
    [JsonPropertyName("frequency")]
    public int Frequency { get; set; }
    [JsonPropertyName("assists")]
    public DetailResponseAssists? Assists { get; set; }
    [JsonPropertyName("wrappedPort")]
    public int WrappedPort { get; set; }
    [JsonPropertyName("ambientTemperature")]
    public float AmbientTemperature { get; set; }
    [JsonPropertyName("roadTemperature")]
    public float RoadTemperature { get; set; }
    [JsonPropertyName("currentWeatherId")]
    public string? CurrentWeatherId { get; set; }
    [JsonPropertyName("windSpeed")]
    public int WindSpeed { get; set; }
    [JsonPropertyName("windDirection")]
    public int WindDirection { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("grip")]
    public float Grip { get; set; }
    [JsonPropertyName("gripTransfer")]
    public float GripTransfer { get; set; }
    [JsonPropertyName("features")]
    public IEnumerable<string>? Features { get; set; }
    [JsonPropertyName("loadingImageUrl")]
    public string? LoadingImageUrl { get; set; }

    [JsonPropertyName("extensions")]
    public Dictionary<string, object>? Extensions { get; set; }
}

public class DetailResponseAssists
{
    [JsonPropertyName("absState")]
    public int AbsState { get; set; }
    [JsonPropertyName("tcState")]
    public int TcState { get; set; }
    [JsonPropertyName("fuelRate")]
    public int FuelRate { get; set; }
    [JsonPropertyName("damageMultiplier")]
    public int DamageMultiplier { get; set; }
    [JsonPropertyName("tyreWearRate")]
    public int TyreWearRate { get; set; }
    [JsonPropertyName("allowedTyresOut")]
    public short AllowedTyresOut { get; set; }
    [JsonPropertyName("stabilityAllowed")]
    public bool StabilityAllowed { get; set; }
    [JsonPropertyName("autoclutchAllowed")]
    public bool AutoclutchAllowed { get; set; }
    [JsonPropertyName("tyreBlanketsAllowed")]
    public bool TyreBlanketsAllowed { get; set; }
    [JsonPropertyName("forceVirtualMirror")]
    public bool ForceVirtualMirror { get; set; }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class DetailResponseCar : EntryListResponseCar
{
    [JsonPropertyName("DriverNation")]
    public string? DriverNation { get; set; }
    [JsonPropertyName("ID")]
    public string? ID { get; set; }
}

public class DetailResponsePlayerList
{
    [JsonPropertyName("Cars")]
    public required IEnumerable<DetailResponseCar> Cars { get; set; }
}
