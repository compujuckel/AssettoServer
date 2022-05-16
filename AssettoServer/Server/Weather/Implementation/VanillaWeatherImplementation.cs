using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Tcp;
using NodaTime;

namespace AssettoServer.Server.Weather.Implementation;

public class VanillaWeatherImplementation : IWeatherImplementation
{
    private readonly EntryCarManager _entryCarManager;
    private readonly IWeatherTypeProvider _weatherTypeProvider;
    private WeatherUpdate? _lastWeather;

    public VanillaWeatherImplementation(IWeatherTypeProvider weatherTypeProvider, EntryCarManager entryCarManager)
    {
        _weatherTypeProvider = weatherTypeProvider;
        _entryCarManager = entryCarManager;
    }
    
    public void SendWeather(WeatherData weather, ZonedDateTime dateTime, ACTcpClient? client = null)
    {
        var wfxParams = new WeatherFxParams
        {
            Type = weather.Type.WeatherFxType,
            StartDate = dateTime.Date.AtStartOfDayInZone(DateTimeZone.Utc).ToInstant().ToUnixTimeSeconds()
        };

        var weatherType = _weatherTypeProvider.GetWeatherType(wfxParams.Type) with
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
                _entryCarManager.BroadcastPacket(weatherUpdate);
            }

            _entryCarManager.BroadcastPacket(PrepareSunAngleUpdate(dateTime));
        }
        else
        {
            client.SendPacket(weatherUpdate);
            client.SendPacket(PrepareSunAngleUpdate(dateTime));
        }

        _lastWeather = weatherUpdate;
    }

    private SunAngleUpdate PrepareSunAngleUpdate(ZonedDateTime dateTime)
    {
        return new SunAngleUpdate
        {
            SunAngle = (float)WeatherUtils.SunAngleFromTicks(dateTime.TimeOfDay.TickOfDay)
        };
    }
}
