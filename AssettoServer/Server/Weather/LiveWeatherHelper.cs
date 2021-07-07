using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Server.Weather
{
    public class LiveWeatherHelper
    {
        private readonly IWeatherTypeProvider _weatherTypeProvider;
        private readonly IWeatherProvider _weatherProvider;

        private readonly float _lat;
        private readonly float _lon;

        public static readonly int TimeMinimum = 8 * 60 * 60;
        public static readonly int TimeMaximum = 18 * 60 * 60;

        public LiveWeatherHelper(IWeatherTypeProvider weatherTypeProvider, IWeatherProvider weatherProvider, float lat, float lon)
        {
            _weatherTypeProvider = weatherTypeProvider;
            _weatherProvider = weatherProvider;

            _lat = lat;
            _lon = lon;
        }

        public async Task<WeatherData> UpdateAsync(int seconds)
        {
            var response = await _weatherProvider.GetWeatherAsync(_lat, _lon);

            var weatherType = _weatherTypeProvider.GetWeatherType(response.WeatherType);

            return new WeatherData
            {
                Graphics = weatherType.Graphics,
                TemperatureAmbient = response.TemperatureAmbient,
                TemperatureRoad = (float)GetRoadTemperature(seconds, response.TemperatureAmbient, weatherType.TemperatureCoefficient),
                Pressure = response.Pressure,
                Humidity = response.Humidity,
                WindSpeed = response.WindSpeed,
                WindDirection = response.WindDirection
            };
        }

        // From https://github.com/gro-ove/actools/blob/master/AcTools/Processes/Game.Properties.cs#L481
        public static double GetRoadTemperature(double seconds, double ambientTemperature, double weatherCoefficient = 1.0)
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
