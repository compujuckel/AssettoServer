using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AssettoServer.Shared.Http.Responses;

public class DetailResponse : InfoResponse
{
    public required DetailResponsePlayerList Players { get; set; }
    public long Until { get; set; }
    public CMContentConfiguration? Content { get; set; }
    public string? TrackBase { get; set; }
    public string? City { get; set; }
    public int Frequency { get; set; }
    public DetailResponseAssists? Assists { get; set; }
    public int WrappedPort { get; set; }
    public float AmbientTemperature { get; set; }
    public float RoadTemperature { get; set; }
    public string? CurrentWeatherId { get; set; }
    public int WindSpeed { get; set; }
    public int WindDirection { get; set; }
    public string? Description { get; set; }
    public float Grip { get; set; }
    public float GripTransfer { get; set; }
    public IEnumerable<string>? Features { get; set; }
}

public class DetailResponseAssists
{
    public int AbsState { get; set; }
    public int TcState { get; set; }
    public int FuelRate { get; set; } 
    public int DamageMultiplier { get; set; }
    public int TyreWearRate { get; set; }
    public short AllowedTyresOut { get; set; }
    public bool StabilityAllowed { get; set; }
    public bool AutoclutchAllowed { get; set; }
    public bool TyreBlanketsAllowed { get; set; }
    public bool ForceVirtualMirror { get; set; }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class DetailResponseCar : EntryListResponseCar
{
    public string? DriverNation { get; set; }
    public string? ID { get; set; }
}

[JsonObject(NamingStrategyType = typeof(DefaultNamingStrategy))]
public class DetailResponsePlayerList
{
    public required IEnumerable<DetailResponseCar> Cars { get; set; }
}
