using System.Globalization;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace TimeDilationPlugin;

public class TimeDilationPlugin : BackgroundService
{
    private readonly WeatherManager _weatherManager;
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly TimeDilationConfiguration _configuration;

    public TimeDilationPlugin(TimeDilationConfiguration configuration,
        WeatherManager weatherManager,
        ACServerConfiguration serverConfiguration)
    {
        _weatherManager = weatherManager;
        _serverConfiguration = serverConfiguration;
        _configuration = configuration;
    }
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _configuration.Mode == TimeDilationMode.Time
            ? TimeBasedTimeDilation(stoppingToken)
            : SunPositionTimeDilation(stoppingToken);
    }

    private async Task SunPositionTimeDilation(CancellationToken stoppingToken)
    {
        if (!_weatherManager.CurrentSunPosition.HasValue)
        {
            Log.Error("TimeDilationPlugin cannot get current sun position, aborting");
            return;
        }

        var lookupTable = CreateSunAngleBasedLookupTable(_configuration.SunAngleLookupTable);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            try
            {
                var sunAltitudeDeg = _weatherManager.CurrentSunPosition.Value.Altitude * 180.0 / Math.PI;
                _serverConfiguration.Server.TimeOfDayMultiplier = (float)lookupTable.GetValue(sunAltitudeDeg);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during time dilation update");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TimeBasedTimeDilation(CancellationToken stoppingToken)
    {
        var lookupTable = CreateTimeBasedLookupTable(_configuration.TimeLookupTable);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            try
            {
                var liveTime = _weatherManager.CurrentDateTime.TimeOfDay.TickOfDay / 10_000_000.0;
                _serverConfiguration.Server.TimeOfDayMultiplier = (float)lookupTable.GetValue(liveTime);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during time dilation update");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private static LookupTable CreateSunAngleBasedLookupTable(List<SunAngleLUTEntry> entries)
    {
        return new LookupTable(entries
            .Select(entry => new KeyValuePair<double, double>(entry.SunAngle, entry.TimeMult))
            .ToList());
    }

    private static LookupTable CreateTimeBasedLookupTable(List<TimeLUTEntry> entries)
    {
        return new LookupTable(entries
            .Select(entry => 
                new KeyValuePair<double, double>(
                    DateTime.ParseExact(entry.Time, "H:mm", CultureInfo.InvariantCulture).TimeOfDay.TotalSeconds, 
                    entry.TimeMult)
            ).ToList(), 86400);
    }
}
