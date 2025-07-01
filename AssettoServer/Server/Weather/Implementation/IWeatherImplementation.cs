using AssettoServer.Network.Tcp;
using NodaTime;

namespace AssettoServer.Server.Weather.Implementation;

public interface IWeatherImplementation
{
    public void SendWeather(WeatherData weather, ZonedDateTime dateTime, PlayerClient? client = null);
}