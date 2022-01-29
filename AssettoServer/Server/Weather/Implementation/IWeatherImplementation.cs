using AssettoServer.Network.Tcp;

namespace AssettoServer.Server.Weather.Implementation;

public interface IWeatherImplementation
{
    public void SendWeather(ACTcpClient? client = null);
}