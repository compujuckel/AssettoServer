using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AssettoServer.Server.Ai;

public class DynamicTrafficDensity : BackgroundService
{
    private readonly ACServerConfiguration _configuration;
    private readonly WeatherManager _weatherManager;

    public DynamicTrafficDensity(ACServerConfiguration configuration, WeatherManager weatherManager)
    {
        _configuration = configuration;
        _weatherManager = weatherManager;
    }

    private float GetDensity(double hourOfDay)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (Math.Truncate(hourOfDay) == hourOfDay)
        {
            return _configuration.Extra.AiParams.HourlyTrafficDensity![(int)hourOfDay];
        }

        int lowerBound = (int)Math.Floor(hourOfDay);
        int higherBound = (int)Math.Ceiling(hourOfDay) % 24;

        return (float)MathUtils.Lerp(_configuration.Extra.AiParams.HourlyTrafficDensity![lowerBound], _configuration.Extra.AiParams.HourlyTrafficDensity![higherBound], hourOfDay - lowerBound);
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (_configuration.Server.TimeOfDayMultiplier == 0 )
        {
            throw new ConfigurationException("TIME_OF_DAY_MULT in server_cfg.ini must be greater than 0");
        }

        foreach (var wfxParam in _configuration.Server.Weathers.Where(w => w.WeatherFxParams.TimeMultiplier.HasValue))
        {
            if (wfxParam.WeatherFxParams.TimeMultiplier == 0)
            {
                throw new ConfigurationException($"Weather '{wfxParam.Graphics}' must have a 'mult' greater than 0");
            }
        }
        
        return base.StartAsync(cancellationToken);  
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                double hours = _weatherManager.CurrentDateTime.TimeOfDay.TickOfDay / 10_000_000.0 / 3600.0;
                _configuration.Extra.AiParams.TrafficDensity = GetDensity(hours);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in dynamic traffic density update");
            }
            finally
            {
                await Task.Delay(TimeSpan.FromMinutes(10.0 / _configuration.Server.TimeOfDayMultiplier), stoppingToken);
            }
        }
    }
}
