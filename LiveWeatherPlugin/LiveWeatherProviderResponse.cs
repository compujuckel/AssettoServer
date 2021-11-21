using AssettoServer.Server.Weather;

namespace LiveWeatherPlugin;

public class LiveWeatherProviderResponse
{
    public WeatherFxType WeatherType { get; init; }
    public float TemperatureAmbient { get; init; }
    public int Pressure { get; init; }
    public int Humidity { get; init; }
    public float WindSpeed { get; init; }
    public int WindDirection { get; init; }
}