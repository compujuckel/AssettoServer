using System;
using System.Diagnostics.CodeAnalysis;
using AssettoServer.Server.Configuration;
using NodaTime;
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

            int config = Random.Shared.Next(_server.Configuration.Server.Weathers.Count);
            if (!SetWeatherConfiguration(config))
                throw new InvalidOperationException("Could not set initial weather configuration");
        }

        [MemberNotNullWhen(true, nameof(_weatherConfiguration))]
        public bool SetWeatherConfiguration(int id)
        {
            if (id < 0 || id >= _server.Configuration.Server.Weathers.Count)
                return false;
            
            _weatherConfiguration = _server.Configuration.Server.Weathers[id];

            if (_weatherConfiguration.WeatherFxParams.StartTime != null
                || _weatherConfiguration.WeatherFxParams.TimeMultiplier != null)
            {
                Log.Warning("Do not use WeatherFX start times or time multipliers. Use the original config values instead");
            }

            var startDate = _weatherConfiguration.WeatherFxParams.StartDate.HasValue
                ? Instant.FromUnixTimeSeconds(_weatherConfiguration.WeatherFxParams.StartDate.Value)
                : SystemClock.Instance.GetCurrentInstant();
            _server.CurrentDateTime = _server.CurrentDateTime.TimeOfDay.On(startDate.InUtc().Date).InZoneLeniently(_server.CurrentDateTime.Zone);
            _server.UpdateSunPosition();

            var weatherType = _server.WeatherTypeProvider.GetWeatherType(_weatherConfiguration.WeatherFxParams.Type);

            float ambient = GetFloatWithVariation(_weatherConfiguration.BaseTemperatureAmbient, _weatherConfiguration.VariationAmbient);
            
            _server.SetWeather(new WeatherData(weatherType, weatherType)
            {
                TemperatureAmbient = ambient,
                TemperatureRoad = GetFloatWithVariation(ambient + _weatherConfiguration.BaseTemperatureRoad, _weatherConfiguration.VariationRoad),
                WindSpeed = GetRandomFloatInRange(_weatherConfiguration.WindBaseSpeedMin, _weatherConfiguration.WindBaseSpeedMax),
                WindDirection = (int) Math.Round(GetFloatWithVariation(_weatherConfiguration.WindBaseDirection, _weatherConfiguration.WindVariationDirection)),
                RainIntensity = weatherType.RainIntensity,
                RainWater = weatherType.RainWater,
                RainWetness = weatherType.RainWetness,
                TrackGrip = _server.Configuration.Server.DynamicTrack?.BaseGrip ?? 1
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
