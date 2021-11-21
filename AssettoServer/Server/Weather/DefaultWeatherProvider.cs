using System;
using AssettoServer.Server.Configuration;
using Serilog;

namespace AssettoServer.Server.Weather
{
    public class DefaultWeatherProvider
    {
        private readonly ACServer _server;

        private WeatherConfiguration _weatherConfiguration;

        public DefaultWeatherProvider(ACServer server)
        {
            _server = server;

            int config = Random.Shared.Next(_server.Configuration.Weathers.Count);
            SetWeatherConfiguration(config);
        }

        public bool SetWeatherConfiguration(int id)
        {
            if (id < 0 || id >= _server.Configuration.Weathers.Count)
                return false;
            
            _weatherConfiguration = _server.Configuration.Weathers[id];

            if (_weatherConfiguration.WeatherFxParams.StartTime != null
                || _weatherConfiguration.WeatherFxParams.TimeMultiplier != null)
            {
                Log.Warning("Do not use WeatherFX start times or time multipliers. Use the original config values instead");
            }

            DateTimeOffset offset = _weatherConfiguration.WeatherFxParams.StartDate.HasValue ? DateTimeOffset.FromUnixTimeSeconds(_weatherConfiguration.WeatherFxParams.StartDate.Value) : DateTime.UtcNow.Date;

            _server.CurrentDateTime = TimeZoneInfo.ConvertTimeToUtc(offset.DateTime + TimeZoneInfo.ConvertTimeFromUtc(_server.CurrentDateTime, _server.TimeZone).TimeOfDay, _server.TimeZone);

            var weatherType = _server.WeatherTypeProvider.GetWeatherType(_weatherConfiguration.WeatherFxParams.Type);

            _server.SetWeather(new WeatherData
            {
                Type = weatherType,
                UpcomingType = weatherType,
                TemperatureAmbient = GetFloatWithVariation(_weatherConfiguration.BaseTemperatureAmbient, _weatherConfiguration.VariationAmbient),
                TemperatureRoad = GetFloatWithVariation(_weatherConfiguration.BaseTemperatureRoad, _weatherConfiguration.VariationRoad),
                WindSpeed = GetRandomFloatInRange(_weatherConfiguration.WindBaseSpeedMin, _weatherConfiguration.WindBaseSpeedMax),
                WindDirection = (int) Math.Round(GetFloatWithVariation(_weatherConfiguration.WindBaseDirection, _weatherConfiguration.WindVariationDirection)),
                RainIntensity = weatherType.RainIntensity,
                RainWater = weatherType.RainWater,
                RainWetness = weatherType.RainWetness,
                TrackGrip = _server.Configuration.DynamicTrack.BaseGrip
            });

            return true;
        }

        private float GetFloatWithVariation(float baseValue, float variation)
        {
            return GetRandomFloatInRange(baseValue - variation / 2, baseValue + variation / 2);
        }

        private float GetRandomFloatInRange(float min, float max)
        {
            return (float) (Random.Shared.NextDouble() * (max - min) + min);
        }
    }
}