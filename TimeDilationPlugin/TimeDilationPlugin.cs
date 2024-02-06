using System.Globalization;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Services;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace TimeDilationPlugin;

public class TimeDilationPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly WeatherManager _weatherManager;
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly TimeDilationConfiguration _configuration;

    public TimeDilationPlugin(TimeDilationConfiguration configuration, WeatherManager weatherManager, ACServerConfiguration serverConfiguration, IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _weatherManager = weatherManager;
        _serverConfiguration = serverConfiguration;
        _configuration = configuration;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        if (_configuration.Mode == TimeDilationMode.Time)
        {
            await TimeBasedTimeDilation(stoppingToken);
        }
        else
        {
            if (!_weatherManager.CurrentSunPosition.HasValue)
            {
                Log.Error("TimeDilationPlugin cannot get current sun position, aborting");
                return;
            }
            
            await SunPositionTimeDilation(stoppingToken);
        }
    }

    private async Task SunPositionTimeDilation(CancellationToken stoppingToken)
    {
        var lookupTable = new LookupTable(_configuration.SunAngleLookupTable.
            Select(entry => new KeyValuePair<double, double>(entry.SunAngle, entry.TimeMult)).ToList());
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                double sunAltitudeDeg = _weatherManager.CurrentSunPosition!.Value.Altitude * 180.0 / Math.PI;
                _serverConfiguration.Server.TimeOfDayMultiplier = (float)lookupTable.GetValue(sunAltitudeDeg);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during time dilation update");
            }
            finally
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task TimeBasedTimeDilation(CancellationToken stoppingToken)
    {
        var lookupTable = new LookupTable(_configuration.TimeLookupTable
            .Select(entry => 
                new KeyValuePair<double, double>(
                    DateTime.ParseExact(entry.Time, "H:mm", CultureInfo.InvariantCulture).TimeOfDay.TotalSeconds, 
                    entry.TimeMult)
            ).ToList());

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var liveTime = _weatherManager.CurrentDateTime.TimeOfDay;
                double currentTime = new TimeSpan(liveTime.Hour, liveTime.Minute, liveTime.Second).TotalSeconds;
                _serverConfiguration.Server.TimeOfDayMultiplier = (float)lookupTable.GetValue(currentTime);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during time dilation update");
            }
            finally
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
