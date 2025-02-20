using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Services;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;
using TrafficAIPlugin.Configuration;

namespace TrafficAIPlugin;

public class DynamicTrafficDensity : CriticalBackgroundService
{
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly TrafficAiConfiguration _configuration;
    private readonly WeatherManager _weatherManager;

    public DynamicTrafficDensity(ACServerConfiguration serverConfiguration,
        TrafficAiConfiguration configuration,
        WeatherManager weatherManager,
        IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _serverConfiguration = serverConfiguration;
        _configuration = configuration;
        _weatherManager = weatherManager;
    }

    private float GetDensity(double hourOfDay)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (Math.Truncate(hourOfDay) == hourOfDay)
        {
            return _configuration.HourlyTrafficDensity![(int)hourOfDay];
        }

        int lowerBound = (int)Math.Floor(hourOfDay);
        int higherBound = (int)Math.Ceiling(hourOfDay) % 24;

        return (float)MathUtils.Lerp(_configuration.HourlyTrafficDensity![lowerBound], _configuration.HourlyTrafficDensity![higherBound], hourOfDay - lowerBound);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_configuration.HourlyTrafficDensity == null) return;
        
        if (_serverConfiguration.Server.TimeOfDayMultiplier == 0 )
        {
            throw new ConfigurationException("TIME_OF_DAY_MULT in server_cfg.ini must be greater than 0");
        }

        foreach (var wfxParam in _serverConfiguration.Server.Weathers.Where(w => w.WeatherFxParams.TimeMultiplier.HasValue))
        {
            if (wfxParam.WeatherFxParams.TimeMultiplier == 0)
            {
                throw new ConfigurationException($"Weather '{wfxParam.Graphics}' must have a 'mult' greater than 0");
            }
        }
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                double hours = _weatherManager.CurrentDateTime.TimeOfDay.TickOfDay / 10_000_000.0 / 3600.0;
                _configuration.TrafficDensity = GetDensity(hours);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in dynamic traffic density update");
            }
            finally
            {
                await Task.Delay(TimeSpan.FromMinutes(10.0 / _serverConfiguration.Server.TimeOfDayMultiplier), stoppingToken);
            }
        }
    }
}
