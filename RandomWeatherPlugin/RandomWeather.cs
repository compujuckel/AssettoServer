using AssettoServer.Server.Plugin;
using AssettoServer.Server.Weather;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace RandomWeatherPlugin;

public class RandomWeather : BackgroundService, IAssettoServerAutostart
{
    private readonly WeatherManager _weatherManager;
    private readonly IWeatherTypeProvider _weatherTypeProvider;
    private readonly RandomWeatherConfiguration _configuration;
    private readonly List<WeatherFxType> _availableWeathers;

    public RandomWeather(RandomWeatherConfiguration configuration, WeatherManager weatherManager, IWeatherTypeProvider weatherTypeProvider)
    {
        _configuration = configuration;
        _weatherManager = weatherManager;
        _weatherTypeProvider = weatherTypeProvider;

        if (!_configuration.BlacklistedWeathers.Contains(WeatherFxType.None))
        {
            _configuration.BlacklistedWeathers.Add(WeatherFxType.None);
        }

        _availableWeathers = Enum.GetValues<WeatherFxType>().Except(_configuration.BlacklistedWeathers).ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int weatherDuration = 1000;
        int transitionDuration = 1000;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                weatherDuration = Random.Shared.Next(_configuration.MinWeatherDurationMilliseconds, _configuration.MaxWeatherDurationMilliseconds);
                transitionDuration = Random.Shared.Next(_configuration.MinTransitionDurationMilliseconds, _configuration.MaxTransitionDurationMilliseconds);
                var nextWeatherType = _weatherTypeProvider.GetWeatherType(_availableWeathers[Random.Shared.Next(_availableWeathers.Count)]);

                var last = _weatherManager.CurrentWeather;

                Log.Information("Random weather transitioning to {WeatherType}, transition duration {TransitionDuration} seconds, weather duration {WeatherDuration} minutes", 
                    nextWeatherType.WeatherFxType, 
                    Math.Round(transitionDuration / 1000.0f), 
                    Math.Round(weatherDuration / 60_000.0f, 1));
                
                _weatherManager.SetWeather(new WeatherData(last.Type, nextWeatherType)
                {
                    TransitionDuration = transitionDuration,
                    TemperatureAmbient = last.TemperatureAmbient,
                    TemperatureRoad = (float)WeatherUtils.GetRoadTemperature(_weatherManager.CurrentDateTime.TimeOfDay.TickOfDay / 10_000_000.0, last.TemperatureAmbient,
                        nextWeatherType.TemperatureCoefficient),
                    Pressure = last.Pressure,
                    Humidity = (int)(nextWeatherType.Humidity * 100),
                    WindSpeed = last.WindSpeed,
                    WindDirection = last.WindDirection,
                    RainIntensity = last.RainIntensity,
                    RainWetness = last.RainWetness,
                    RainWater = last.RainWater,
                    TrackGrip = last.TrackGrip
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during random weather update");
            }
            finally
            {
                await Task.Delay(transitionDuration + weatherDuration, stoppingToken);
            }
        }
    }
}
