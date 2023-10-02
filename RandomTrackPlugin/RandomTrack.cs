using AssettoServer.Server.Plugin;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Services;
using AssettoServer.Shared.Weather;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace RandomTrackPlugin;

public class RandomTrack : CriticalBackgroundService, IAssettoServerAutostart
{
    private struct TrackWeight
    {
        internal string Track { get; init; }
        internal float PrefixSum { get; init; }
    }

    private readonly RandomWeatherConfiguration _configuration;
    private readonly List<TrackWeight> _tracks = new();

    public RandomTrack(RandomTrackConfiguration configuration, WeatherManager weatherManager, IWeatherTypeProvider weatherTypeProvider, IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _configuration = configuration;
        _weatherManager = weatherManager;
        _weatherTypeProvider = weatherTypeProvider;

        foreach (var weather in Enum.GetValues<WeatherFxType>())
        {
            _configuration.WeatherWeights.TryAdd(weather, 1.0f);
        }

        _configuration.WeatherWeights[WeatherFxType.None] = 0;

        float weightSum = _configuration.WeatherWeights
            .Select(w => w.Value)
            .Sum();

        float prefixSum = 0.0f;
        foreach (var (weather, weight) in _configuration.WeatherWeights)
        {
            if (weight > 0)
            {
                prefixSum += weight / weightSum;
                _weathers.Add(new WeatherWeight
                {
                    Weather = weather,
                    PrefixSum = prefixSum,
                });
            }
        }

        _weathers.Sort((a, b) =>
        {
            if (a.PrefixSum < b.PrefixSum)
                return -1;
            if (a.PrefixSum > b.PrefixSum)
                return 1;
            return 0;
        });
    }

    private WeatherFxType PickRandom()
    {
        float rng = Random.Shared.NextSingle();
        WeatherFxType weather = WeatherFxType.None;

        int begin = 0, end = _weathers.Count;
        while (begin <= end)
        {
            int i = (begin + end) / 2;

            if (_weathers[i].PrefixSum <= rng)
            {
                begin = i + 1;
            }
            else
            {
                end = i - 1;
                weather = _weathers[i].Weather;
            }
        }

        return weather;
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

                WeatherType nextWeatherType = _weatherTypeProvider.GetWeatherType(PickRandom());

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
                    Humidity = nextWeatherType.Humidity,
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
