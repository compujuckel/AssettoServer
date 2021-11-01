using System;
using System.Threading.Tasks;
using Serilog;

namespace AssettoServer.Server.Weather
{
    public class LiveWeatherProvider : IWeatherProvider
    {
        private readonly ACServer _server;
        private readonly ILiveWeatherProvider _liveWeatherProvider;

        public LiveWeatherProvider(ACServer server)
        {
            _server = server;
            
            if (string.IsNullOrWhiteSpace(server.Configuration.Extra.OwmApiKey))
                throw new Exception("OpenWeatherMap API key not set. Cannot enable live weather");
            _liveWeatherProvider = new OpenWeatherMapWeatherProvider(_server.Configuration.Extra.OwmApiKey);
        }
        
        public async Task UpdateAsync(WeatherData last = null)
        {
            Log.Debug("Updating live weather...");
            var response = await _liveWeatherProvider.GetWeatherAsync(_server.TrackParams.Latitude, _server.TrackParams.Longitude);

            var weatherType = _server.WeatherTypeProvider.GetWeatherType(response.WeatherType);

            if (_server.WeatherFxStartDate.HasValue)
            {
                weatherType = weatherType with
                {
                    Graphics = new WeatherFxParams()
                    {
                        Type = weatherType.WeatherFxType,
                        StartDate = _server.WeatherFxStartDate
                    }.ToString()
                };
            }

            if (last == null)
            {
                _server.SetWeather(new WeatherData
                {
                    Type = weatherType,
                    UpcomingType = weatherType,
                    TemperatureAmbient = response.TemperatureAmbient,
                    TemperatureRoad = (float)WeatherUtils.GetRoadTemperature(_server.CurrentDaySeconds, response.TemperatureAmbient, weatherType.TemperatureCoefficient),
                    Pressure = response.Pressure,
                    Humidity = response.Humidity,
                    WindSpeed = response.WindSpeed,
                    WindDirection = response.WindDirection,
                    RainIntensity = weatherType.RainIntensity,
                    RainWetness = weatherType.RainWetness,
                    RainWater = weatherType.RainWater,
                    TrackGrip = _server.Configuration.DynamicTrack.BaseGrip
                });
            }
            else
            {
                _server.SetWeather(new WeatherData
                {
                    Type = last.Type,
                    UpcomingType = weatherType,
                    TransitionDuration = 120000.0,
                    TemperatureAmbient = response.TemperatureAmbient,
                    TemperatureRoad = (float)WeatherUtils.GetRoadTemperature(_server.CurrentDaySeconds, response.TemperatureAmbient, weatherType.TemperatureCoefficient),
                    Pressure = response.Pressure,
                    Humidity = response.Humidity,
                    WindSpeed = response.WindSpeed,
                    WindDirection = response.WindDirection,
                    RainIntensity = last.RainIntensity,
                    RainWetness = last.RainWetness,
                    RainWater = last.RainWater,
                    TrackGrip = last.TrackGrip
                });
            }
        }
    }
}