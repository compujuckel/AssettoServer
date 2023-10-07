using AssettoServer.Shared.Weather;

namespace AssettoServer.Server.Weather;

public record WeatherType
{
    public WeatherFxType WeatherFxType { get; init; }
    public string? Graphics { get; init; }
    public float TemperatureCoefficient { get; init; }
    public float RainIntensity { get; init; }
    public float RainWetness { get; init; }
    public float RainWater { get; init; }
    public float Sun { get; init; }
    public float Humidity { get; init; }
}
