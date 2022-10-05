using System;
using System.Diagnostics.CodeAnalysis;
using AssettoServer.Server.Configuration;
using NodaTime;
using Serilog;

namespace AssettoServer.Server.Weather;

public class DefaultWeatherProvider
{
    private readonly ACServerConfiguration _configuration;
    private readonly WeatherManager _weatherManager;
    private readonly IWeatherTypeProvider _weatherTypeProvider;

    private WeatherConfiguration _weatherConfiguration;

    public DefaultWeatherProvider(WeatherManager weatherManager, IWeatherTypeProvider weatherTypeProvider, ACServerConfiguration configuration)
    {
        _weatherManager = weatherManager;
        _weatherTypeProvider = weatherTypeProvider;
        _configuration = configuration;

        int config = Random.Shared.Next(_configuration.Server.Weathers.Count);
        if (!SetWeatherConfiguration(config))
            throw new InvalidOperationException("Could not set initial weather configuration");
    }

    [MemberNotNullWhen(true, nameof(_weatherConfiguration))]
    public bool SetWeatherConfiguration(int id)
    {
        if (id < 0 || id >= _configuration.Server.Weathers.Count)
            return false;
            
        _weatherConfiguration = _configuration.Server.Weathers[id];

        if (_weatherConfiguration.WeatherFxParams.StartTime != null
            || _weatherConfiguration.WeatherFxParams.TimeMultiplier != null)
        {
            Log.Warning("Do not use WeatherFX start times or time multipliers. Use the original config values instead");
        }

        var startDate = _weatherConfiguration.WeatherFxParams.StartDate.HasValue
            ? Instant.FromUnixTimeSeconds(_weatherConfiguration.WeatherFxParams.StartDate.Value)
            : SystemClock.Instance.GetCurrentInstant();
        _weatherManager.CurrentDateTime = _weatherManager.CurrentDateTime.TimeOfDay.On(startDate.InUtc().Date).InZoneLeniently(_weatherManager.CurrentDateTime.Zone);

        var weatherType = _weatherTypeProvider.GetWeatherType(_weatherConfiguration.WeatherFxParams.Type);

        float ambient = GetFloatWithVariation(_weatherConfiguration.BaseTemperatureAmbient, _weatherConfiguration.VariationAmbient);
            
        _weatherManager.SetWeather(new WeatherData(weatherType, weatherType)
        {
            TemperatureAmbient = ambient,
            TemperatureRoad = GetFloatWithVariation(ambient + _weatherConfiguration.BaseTemperatureRoad, _weatherConfiguration.VariationRoad),
            WindSpeed = GetRandomFloatInRange(_weatherConfiguration.WindBaseSpeedMin, _weatherConfiguration.WindBaseSpeedMax),
            WindDirection = (int) Math.Round(GetFloatWithVariation(_weatherConfiguration.WindBaseDirection, _weatherConfiguration.WindVariationDirection)),
            RainIntensity = weatherType.RainIntensity,
            RainWater = weatherType.RainWater,
            RainWetness = weatherType.RainWetness,
            TrackGrip = _configuration.Server.DynamicTrack.BaseGrip
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
