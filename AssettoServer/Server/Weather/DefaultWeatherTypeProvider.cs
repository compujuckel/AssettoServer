using System.Collections.Immutable;

namespace AssettoServer.Server.Weather
{
    public class DefaultWeatherTypeProvider : IWeatherTypeProvider
    {
        private static readonly ImmutableList<WeatherType> WeatherTypes = ImmutableList.Create<WeatherType>(
            new()
            {
                WeatherFxType = WeatherFxType.LightThunderstorm,
                TemperatureCoefficient = 0.7f,
                RainIntensity = 0.1f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 0.20f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Thunderstorm,
                TemperatureCoefficient = 0.2f,
                RainIntensity = 0.2f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 0.10f
            },
            new()
            {
                WeatherFxType = WeatherFxType.HeavyThunderstorm,
                TemperatureCoefficient = -0.2f,
                RainIntensity = 0.4f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 0.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.LightDrizzle,
                TemperatureCoefficient = 0.1f,
                RainIntensity = 0.05f,
                RainWetness = 0.05f,
                RainWater = 0.00f,
                Sun = 0.50f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Drizzle,
                TemperatureCoefficient = -0.1f,
                RainIntensity = 0.15f,
                RainWetness = 0.10f,
                RainWater = 0.05f,
                Sun = 0.10f
            },
            new()
            {
                WeatherFxType = WeatherFxType.HeavyDrizzle,
                TemperatureCoefficient = -0.3f,
                RainIntensity = 0.25f,
                RainWetness = 0.20f,
                RainWater = 0.10f,
                Sun = 0.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.LightRain,
                TemperatureCoefficient = 0.01f,
                RainIntensity = 0.3f,
                RainWetness = 0.30f,
                RainWater = 0.10f,
                Sun = 0.25f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Rain,
                TemperatureCoefficient = -0.2f,
                RainIntensity = 0.6f,
                RainWetness = 0.60f,
                RainWater = 0.30f,
                Sun = 0.05f
            },
            new()
            {
                WeatherFxType = WeatherFxType.HeavyRain,
                TemperatureCoefficient = -0.5f,
                RainIntensity = 1.0f,
                RainWetness = 1.00f,
                RainWater = 0.50f,
                Sun = 0.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.LightSnow,
                TemperatureCoefficient = -0.7f,
                RainIntensity = 0.0f,
                RainWetness = 0.20f,
                RainWater = 0.05f,
                Sun = 0.50f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Snow,
                TemperatureCoefficient = -0.8f,
                RainIntensity = 0.0f,
                RainWetness = 0.30f,
                RainWater = 0.10f,
                Sun = 0.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.HeavySnow,
                TemperatureCoefficient = -0.9f,
                RainIntensity = 0.0f,
                RainWetness = 0.40f,
                RainWater = 0.15f,
                Sun = 0.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.LightSleet,
                TemperatureCoefficient = -1f,
                RainIntensity = 0.2f,
                RainWetness = 0.30f,
                RainWater = 0.10f,
                Sun = 0.10f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Sleet,
                TemperatureCoefficient = -1f,
                RainIntensity = 0.5f,
                RainWetness = 0.50f,
                RainWater = 0.20f,
                Sun = 0.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.HeavySleet,
                TemperatureCoefficient = -1f,
                RainIntensity = 0.8f,
                RainWetness = 0.70f,
                RainWater = 0.30f,
                Sun = 0.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Clear,
                TemperatureCoefficient = 1f,
                RainIntensity = 0.0f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 1.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.FewClouds,
                TemperatureCoefficient = 1f,
                RainIntensity = 0.0f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 1.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.ScatteredClouds,
                TemperatureCoefficient = 0.8f,
                RainIntensity = 0.0f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 0.75f
            },
            new()
            {
                WeatherFxType = WeatherFxType.BrokenClouds,
                TemperatureCoefficient = 0.1f,
                RainIntensity = 0.0f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 0.50f
            },
            new()
            {
                WeatherFxType = WeatherFxType.OvercastClouds,
                TemperatureCoefficient = 0.01f,
                RainIntensity = 0.0f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 0.20f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Fog,
                TemperatureCoefficient = -0.3f,
                RainIntensity = 0.0f,
                RainWetness = 0.20f,
                RainWater = 0.00f,
                Sun = 0.50f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Mist,
                TemperatureCoefficient = -0.2f,
                RainIntensity = 0.0f,
                RainWetness = 0.10f,
                RainWater = 0.00f,
                Sun = 0.50f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Smoke,
                TemperatureCoefficient = -0.2f,
                RainIntensity = 0.0f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 0.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Haze,
                TemperatureCoefficient = 0.9f,
                RainIntensity = 0.0f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 1.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Sand,
                TemperatureCoefficient = 1f,
                RainIntensity = 0.0f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 1.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Dust,
                TemperatureCoefficient = 1f,
                RainIntensity = 0.0f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 1.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Squalls,
                TemperatureCoefficient = -0.5f,
                RainIntensity = 0.0f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 0.50f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Tornado,
                TemperatureCoefficient = -0.3f,
                RainIntensity = 0.3f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 0.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Hurricane,
                TemperatureCoefficient = -0.6f,
                RainIntensity = 0.5f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 0.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Cold,
                TemperatureCoefficient = 0.0f, // TODO
                RainIntensity = 0.0f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 1.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Hot,
                TemperatureCoefficient = 0.0f, // TODO
                RainIntensity = 0.0f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 1.00f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Windy,
                TemperatureCoefficient = 0.3f,
                RainIntensity = 0.0f,
                RainWetness = 0.00f,
                RainWater = 0.00f,
                Sun = 0.85f
            },
            new()
            {
                WeatherFxType = WeatherFxType.Hail,
                TemperatureCoefficient = -1f,
                RainIntensity = 0.5f,
                RainWetness = 0.50f,
                RainWater = 0.10f,
                Sun = 0.30f
            }
        );

        public WeatherType GetWeatherType(WeatherFxType id)
        {
            return WeatherTypes.Find(type => type.WeatherFxType == id);
        }
    }
}