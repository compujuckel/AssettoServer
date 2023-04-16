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
    private readonly LookupTable _lookupTable;
    private readonly WeatherManager _weatherManager;
    private readonly ACServerConfiguration _configuration;

    public TimeDilationPlugin(TimeDilationConfiguration configuration, WeatherManager weatherManager, ACServerConfiguration serverConfiguration, IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _weatherManager = weatherManager;
        _configuration = serverConfiguration;
        _lookupTable = new LookupTable(configuration.LookupTable.Select(entry => new KeyValuePair<double, double>(entry.SunAngle, entry.TimeMult)).ToList());
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_weatherManager.CurrentSunPosition.HasValue)
        {
            Log.Error("TimeDilationPlugin cannot get current sun position, aborting");
            return;
        }
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                double sunAltitudeDeg = _weatherManager.CurrentSunPosition.Value.Altitude * 180.0 / Math.PI;
                _configuration.Server.TimeOfDayMultiplier = (float)_lookupTable.GetValue(sunAltitudeDeg);
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
