namespace AssettoServer.Server.Weather;

public interface IWeatherTypeProvider
{
    public WeatherType GetWeatherType(WeatherFxType id);
}
