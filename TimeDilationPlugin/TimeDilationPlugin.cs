using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Utils;
using JetBrains.Annotations;
using Serilog;

namespace TimeDilationPlugin;

[UsedImplicitly]
public class TimeDilationPlugin : IAssettoServerPlugin<TimeDilationConfiguration>
{
    private TimeDilationConfiguration? Configuration { get; set; }
    private ACServer Server { get; set; } = null!;
    private LookupTable LookupTable { get; set; } = null!;
    
    public void SetConfiguration(TimeDilationConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void Initialize(ACServer server)
    {
        Server = server;

        if (Configuration?.LookupTable == null || Configuration.LookupTable.Count == 0)
        {
            throw new ConfigurationException("No configuration found for TimeDilationPlugin or lookup table empty");
        }

        LookupTable = new LookupTable(Configuration.LookupTable.Select(entry => new KeyValuePair<double, double>(entry.SunAngle, entry.TimeMult)).ToList());

        _ = UpdateLoopAsync();
    }

    private async Task UpdateLoopAsync()
    {
        if (!Server.CurrentSunPosition.HasValue)
        {
            Log.Error("TimeDilationPlugin cannot get current sun position, aborting");
            return;
        }
        
        while (true)
        {
            try
            {
                double sunAltitudeDeg = Server.CurrentSunPosition.Value.Altitude * 180.0 / Math.PI;
                Server.Configuration.Server.TimeOfDayMultiplier = (float)LookupTable.GetValue(sunAltitudeDeg);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during time dilation update");
            }
            finally
            {
                await Task.Delay(1000);
            }
        }
    }
}