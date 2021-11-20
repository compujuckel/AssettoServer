using System;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Tcp;

namespace AssettoServer.Server.Weather.Implementation;

public class VanillaWeatherImplementation : IWeatherImplementation
{
    private readonly ACServer _server;
    private WeatherUpdate _lastWeather;

    public VanillaWeatherImplementation(ACServer server)
    {
        _server = server;
    }
    
    public void SendWeather(ACTcpClient client = null)
    {
        var weather = _server.CurrentWeather;
        
        var wfxParams = new WeatherFxParams
        {
            Type = weather.Type.WeatherFxType,
            StartDate = new DateTimeOffset(TimeZoneInfo.ConvertTimeFromUtc(_server.CurrentDateTime, _server.TimeZone).Date, TimeSpan.Zero).ToUnixTimeSeconds()
        };
        
        var weatherType = _server.WeatherTypeProvider.GetWeatherType(wfxParams.Type) with
        {
            Graphics = wfxParams.ToString(),
        };
        
        var weatherUpdate = new WeatherUpdate
        {
            Ambient = (byte) weather.TemperatureAmbient,
            Graphics = weatherType.Graphics,
            Road = (byte) weather.TemperatureRoad,
            WindDirection = (short) weather.WindDirection,
            WindSpeed = (short) weather.WindSpeed
        };

        if (client == null)
        {
            if (_lastWeather == null
                || weatherUpdate.Ambient != _lastWeather.Ambient
                || weatherUpdate.Graphics != _lastWeather.Graphics
                || weatherUpdate.Road != _lastWeather.Road
                || weatherUpdate.WindDirection != _lastWeather.WindDirection
                || weatherUpdate.WindSpeed != _lastWeather.WindSpeed)
            {
                _server.BroadcastPacket(weatherUpdate);
            }

            _server.BroadcastPacket(PrepareSunAngleUpdate());
        }
        else
        {
            client.SendPacket(weatherUpdate);
            client.SendPacket(PrepareSunAngleUpdate());
        }

        _lastWeather = weatherUpdate;
    }

    private SunAngleUpdate PrepareSunAngleUpdate()
    {
        return new SunAngleUpdate
        {
            SunAngle = WeatherUtils.SunAngleFromSeconds((float)TimeZoneInfo.ConvertTimeFromUtc(_server.CurrentDateTime, _server.TimeZone).TimeOfDay.TotalSeconds)
        };
    }
}
