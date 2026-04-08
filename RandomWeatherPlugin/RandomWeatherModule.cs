using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Weather;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace RandomWeatherPlugin;

public class RandomWeatherModule : AssettoServerModule<RandomWeatherConfiguration>
{
    public override RandomWeatherConfiguration ReferenceConfiguration => new RandomWeatherConfiguration
    {
        Mode = RandomWeatherMode.TransitionTable,
        MinWeatherDurationMinutes = 15,
        MaxWeatherDurationMinutes = 60,
        MinTransitionDurationSeconds = 180,
        MaxTransitionDurationSeconds = 600,
        WeatherWeights = new()
        {
            { WeatherFxType.LightThunderstorm, 1.0f },
            { WeatherFxType.Thunderstorm, 1.0f },
            { WeatherFxType.HeavyThunderstorm, 1.0f },
            { WeatherFxType.LightDrizzle, 1.0f },
            { WeatherFxType.Drizzle, 1.0f },
            { WeatherFxType.HeavyDrizzle, 1.0f },
            { WeatherFxType.LightRain, 1.0f },
            { WeatherFxType.Rain, 1.0f },
            { WeatherFxType.HeavyRain, 1.0f },
            { WeatherFxType.LightSnow, 1.0f },
            { WeatherFxType.Snow, 1.0f },
            { WeatherFxType.HeavySnow, 1.0f },
            { WeatherFxType.LightSleet, 1.0f },
            { WeatherFxType.Sleet, 1.0f },
            { WeatherFxType.HeavySleet, 1.0f },
            { WeatherFxType.Clear, 1.0f },
            { WeatherFxType.FewClouds, 1.0f },
            { WeatherFxType.ScatteredClouds, 1.0f },
            { WeatherFxType.BrokenClouds, 1.0f },
            { WeatherFxType.OvercastClouds, 1.0f },
            { WeatherFxType.Fog, 1.0f },
            { WeatherFxType.Mist, 1.0f },
            { WeatherFxType.Smoke, 1.0f },
            { WeatherFxType.Haze, 1.0f },
            { WeatherFxType.Sand, 1.0f },
            { WeatherFxType.Dust, 1.0f },
            { WeatherFxType.Squalls, 1.0f },
            { WeatherFxType.Tornado, 1.0f },
            { WeatherFxType.Hurricane, 1.0f },
            { WeatherFxType.Cold, 1.0f },
            { WeatherFxType.Hot, 1.0f },
            { WeatherFxType.Windy, 1.0f },
            { WeatherFxType.Hail, 1.0f },
        },
        WeatherTransitions =
        {
            { 
                WeatherFxType.ScatteredClouds, new()
                {
                    { WeatherFxType.ScatteredClouds, 3.0f },
                    { WeatherFxType.BrokenClouds, 1.0f },
                    { WeatherFxType.OvercastClouds, 1.0f }
                }
                
            },
            {
                WeatherFxType.BrokenClouds, new()
                {
                    { WeatherFxType.ScatteredClouds, 0.1f },
                    { WeatherFxType.OvercastClouds, 1.0f }
                }
                
            },
            { 
                WeatherFxType.OvercastClouds, new()
                {
                    { WeatherFxType.ScatteredClouds, 1.0f },
                    { WeatherFxType.BrokenClouds, 2.0f },
                    { WeatherFxType.OvercastClouds, 0.2f }
                }
            }
        },
    };
    
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<RandomWeather>().AsSelf().As<IHostedService>().SingleInstance();
    }
}
