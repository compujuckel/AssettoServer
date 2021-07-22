using System;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;

namespace AssettoServer.Server.Weather
{
    public class DefaultWeatherProvider : IWeatherProvider
    {
        private readonly ACServer _server;
        private readonly Random _random;

        private WeatherConfiguration _weatherConfiguration;

        public DefaultWeatherProvider(ACServer server)
        {
            _server = server;
            _random = new Random();

            int config = _random.Next(0, _server.Configuration.Weathers.Count);
            SetWeatherConfiguration(config);
        }

        public bool SetWeatherConfiguration(int id)
        {
            if (id < 0 || id >= _server.Configuration.Weathers.Count)
                return false;
            
            _weatherConfiguration = _server.Configuration.Weathers[id];

            _server.SetWeather(new WeatherData
            {
                Type = new WeatherType
                {
                    WeatherFxType = _weatherConfiguration.WeatherFxParams.Type,
                    Name = _weatherConfiguration.Graphics,
                    Graphics = _weatherConfiguration.Graphics,
                },
                TemperatureAmbient = GetFloatWithVariation(_weatherConfiguration.BaseTemperatureAmbient, _weatherConfiguration.VariationAmbient),
                TemperatureRoad = GetFloatWithVariation(_weatherConfiguration.BaseTemperatureRoad, _weatherConfiguration.VariationRoad),
                WindSpeed = GetRandomFloatInRange(_weatherConfiguration.WindBaseSpeedMin, _weatherConfiguration.WindBaseSpeedMax),
                WindDirection = (int) Math.Round(GetFloatWithVariation(_weatherConfiguration.WindBaseDirection, _weatherConfiguration.WindVariationDirection))
            });

            return true;
        }
        
        public Task UpdateAsync()
        {
            return Task.CompletedTask;
        }

        private float GetFloatWithVariation(float baseValue, float variation)
        {
            return GetRandomFloatInRange(baseValue - variation / 2, baseValue + variation / 2);
        }

        private float GetRandomFloatInRange(float min, float max)
        {
            return (float) (_random.NextDouble() * (max - min) + min);
        }
    }
}