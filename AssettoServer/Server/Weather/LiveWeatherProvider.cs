using System;
using System.Threading.Tasks;

namespace AssettoServer.Server.Weather
{
    public class LiveWeatherProvider : IWeatherProvider
    {
        private readonly ACServer _server;
        private readonly ILiveWeatherProvider _liveWeatherProvider;

        private const int TimeMinimum = 8 * 60 * 60;
        private const int TimeMaximum = 18 * 60 * 60;
        
        public LiveWeatherProvider(ACServer server)
        {
            _server = server;
            
            if (string.IsNullOrWhiteSpace(server.Configuration.Extra.OwmApiKey))
                throw new Exception("OpenWeatherMap API key not set. Cannot enable live weather");
            _liveWeatherProvider = new OpenWeatherMapWeatherProvider(_server.Configuration.Extra.OwmApiKey);
        }
        
        public async Task UpdateAsync()
        {
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

            _server.SetWeather(new WeatherData
            {
                Type = weatherType,
                TemperatureAmbient = response.TemperatureAmbient,
                TemperatureRoad = (float)GetRoadTemperature(_server.CurrentDaySeconds, response.TemperatureAmbient, weatherType.TemperatureCoefficient),
                Pressure = response.Pressure,
                Humidity = response.Humidity,
                WindSpeed = response.WindSpeed,
                WindDirection = response.WindDirection
            });
        }
        
        // From https://github.com/gro-ove/actools/blob/master/AcTools/Processes/Game.Properties.cs#L481
        private static double GetRoadTemperature(double seconds, double ambientTemperature, double weatherCoefficient = 1.0)
        {
            if (seconds < TimeMinimum || seconds > TimeMaximum)
            {
                var minTemperature = GetRoadTemperature(TimeMinimum, ambientTemperature, weatherCoefficient);
                var maxTemperature = GetRoadTemperature(TimeMaximum, ambientTemperature, weatherCoefficient);
                var minValue = TimeMinimum;
                var maxValue = TimeMaximum - 24 * 60 * 60;
                if (seconds > TimeMaximum)
                {
                    seconds -= 24 * 60 * 60;
                }

                return minTemperature + (maxTemperature - minTemperature) * (seconds - minValue) / (maxValue - minValue);
            }

            var time = (seconds / 60d / 60d - 7d) * 0.04167;
            return ambientTemperature * (1d + 5.33332 * (weatherCoefficient == 0d ? 1d : weatherCoefficient) * (1d - time) *
                (Math.Exp(-6d * time) * Math.Sin(6d * time) + 0.25) * Math.Sin(0.9 * time));
        }
    }
}