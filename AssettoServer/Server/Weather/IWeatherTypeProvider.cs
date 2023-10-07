using AssettoServer.Shared.Weather;

namespace AssettoServer.Server.Weather;

public interface IWeatherTypeProvider
{
    public WeatherType GetWeatherType(WeatherFxType id);
}
