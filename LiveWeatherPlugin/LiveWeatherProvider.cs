using AssettoServer.Server.Plugin;
using AssettoServer.Server.TrackParams;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Services;
using AssettoServer.Shared.Weather;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace LiveWeatherPlugin;

public class LiveWeatherProvider : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly LiveWeatherConfiguration _configuration;
    private readonly OpenWeatherMapWeatherProvider _liveWeatherProvider;
    private readonly WeatherManager _weatherManager;
    private readonly IWeatherTypeProvider _weatherTypeProvider;
    private TrackParams _trackParams = null!; 

    public LiveWeatherProvider(LiveWeatherConfiguration configuration, WeatherManager weatherManager, IWeatherTypeProvider weatherTypeProvider, IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _configuration = configuration;
        _weatherManager = weatherManager;
        _weatherTypeProvider = weatherTypeProvider;
        _liveWeatherProvider = new OpenWeatherMapWeatherProvider(_configuration.OpenWeatherMapApiKey);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _trackParams = _weatherManager.TrackParams ?? throw new InvalidOperationException("No track params set for track");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during live weather update");
            }
            finally
            {
                await Task.Delay(_configuration.UpdateIntervalMilliseconds, stoppingToken);
            }
        }
    }

    private async Task UpdateAsync()
    {
        var last = _weatherManager.CurrentWeather;
        var response = await _liveWeatherProvider.GetWeatherAsync(_trackParams.Latitude, _trackParams.Longitude);
        var weatherType = _weatherTypeProvider.GetWeatherType(response.WeatherType);
            
        Log.Debug("Live weather: {WeatherType}, ambient {TemperatureAmbient}°C", response.WeatherType, response.TemperatureAmbient);

        _weatherManager.SetWeather(new WeatherData(last.Type, weatherType)
        {
            TransitionDuration = 120000.0,
            TemperatureAmbient = response.TemperatureAmbient,
            TemperatureRoad = (float)WeatherUtils.GetRoadTemperature(_weatherManager.CurrentDateTime.TimeOfDay.TickOfDay / 10_000_000.0, response.TemperatureAmbient,
                weatherType.TemperatureCoefficient),
            Pressure = response.Pressure,
            Humidity = response.Humidity / 100.0f,
            WindSpeed = response.WindSpeed,
            WindDirection = response.WindDirection,
            RainIntensity = last.RainIntensity,
            RainWetness = last.RainWetness,
            RainWater = last.RainWater,
            TrackGrip = last.TrackGrip
        });
    }
}
