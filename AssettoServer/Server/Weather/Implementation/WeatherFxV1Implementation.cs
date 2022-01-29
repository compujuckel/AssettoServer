using System;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Tcp;

namespace AssettoServer.Server.Weather.Implementation;

public class WeatherFxV1Implementation : IWeatherImplementation
{
    private readonly ACServer _server;

    public WeatherFxV1Implementation(ACServer server)
    {
        _server = server;
    }

    public void SendWeather(ACTcpClient? client = null)
    {
        var weather = new CSPWeatherUpdate
        {
            UnixTimestamp = (ulong) new DateTimeOffset(_server.CurrentDateTime).ToUnixTimeSeconds(),
            WeatherType = (byte) _server.CurrentWeather.Type.WeatherFxType,
            UpcomingWeatherType = (byte) _server.CurrentWeather.UpcomingType.WeatherFxType,
            TransitionValue = _server.CurrentWeather.TransitionValue,
            TemperatureAmbient = (Half) _server.CurrentWeather.TemperatureAmbient,
            TemperatureRoad = (Half) _server.CurrentWeather.TemperatureRoad,
            TrackGrip = (Half) _server.CurrentWeather.TrackGrip,
            WindDirectionDeg = (Half) _server.CurrentWeather.WindDirection,
            WindSpeed = (Half) _server.CurrentWeather.WindSpeed,
            Humidity = (Half) _server.CurrentWeather.Humidity,
            Pressure = (Half) _server.CurrentWeather.Pressure,
            RainIntensity = (Half) _server.CurrentWeather.RainIntensity,
            RainWetness = (Half) _server.CurrentWeather.RainWetness,
            RainWater = (Half) _server.CurrentWeather.RainWater
        };

        if (client == null)
        {
            _server.BroadcastPacketUdp(weather);
        }
        else
        {
            client.SendPacketUdp(weather);
        }
    }
}