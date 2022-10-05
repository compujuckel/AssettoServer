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

public enum WeatherFxType
{
    None = -1,
    LightThunderstorm = 0,
    Thunderstorm = 1,
    HeavyThunderstorm = 2,
    LightDrizzle = 3,
    Drizzle = 4,
    HeavyDrizzle = 5,
    LightRain = 6,
    Rain = 7,
    HeavyRain = 8,
    LightSnow = 9,
    Snow = 10,
    HeavySnow = 11,
    LightSleet = 12,
    Sleet = 13,
    HeavySleet = 14,
    Clear = 15,
    FewClouds = 16,
    ScatteredClouds = 17,
    BrokenClouds = 18,
    OvercastClouds = 19,
    Fog = 20,
    Mist = 21,
    Smoke = 22,
    Haze = 23,
    Sand = 24,
    Dust = 25,
    Squalls = 26,
    Tornado = 27,
    Hurricane = 28,
    Cold = 29,
    Hot = 30,
    Windy = 31,
    Hail = 32
}
