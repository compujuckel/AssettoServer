using System.Text;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Weather;
using Serilog;

namespace ChangeWeatherPlugin;

public class ChangeWeather
{
    private readonly ACServer _server;
    private readonly List<WeatherFxType> _weathers;

    public ChangeWeather(ACServer server)
    {
        _server = server;
        
        _weathers = Enum.GetValues<WeatherFxType>().ToList();
    }

    internal void ProcessChoice(ACTcpClient client, int choice)
    {
        if (choice >= _weathers.Count - 1 || choice < 0)
        {
            client.SendPacket(new ChatMessage { SessionId = 255, Message = "Invalid choice." });
            return;
        }

        var weather = _weathers[choice];
        var weatherType = _server.WeatherTypeProvider.GetWeatherType(weather);
        _server.BroadcastPacket(new ChatMessage { SessionId = 255, Message = $"The weather will be set to {weather}." });

        var last = _server.CurrentWeather;
        _server.SetWeather(new WeatherData(last.Type, weatherType)
        {
            TransitionDuration = 120000.0,
            TemperatureAmbient = last.TemperatureAmbient,
            TemperatureRoad = (float)WeatherUtils.GetRoadTemperature(_server.CurrentDateTime.TimeOfDay.TickOfDay / 10_000_000.0, last.TemperatureAmbient,
                weatherType.TemperatureCoefficient),
            Pressure = last.Pressure,
            Humidity = (int)(weatherType.Humidity * 100),
            WindSpeed = last.WindSpeed,
            WindDirection = last.WindDirection,
            RainIntensity = last.RainIntensity,
            RainWetness = last.RainWetness,
            RainWater = last.RainWater,
            TrackGrip = last.TrackGrip
        });
    }

    internal void GetWeathers(ACTcpClient client)
    {
        client.SendPacket(new ChatMessage { SessionId = 255, Message = "Available weathers:" });
        for (int i = 0; i < _weathers.Count; i++)
        {
            var nextWeather = _weathers[i];
            client.SendPacket(new ChatMessage { SessionId = 255, Message = $"/cw {i} - {nextWeather}" });
        }
    }
}
