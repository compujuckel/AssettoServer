using System;
using System.Threading.Tasks;
using AssettoServer.Utils;
using Serilog;

namespace AssettoServer.Server.Ai;

public class DynamicTrafficDensity
{
    private readonly ACServer _server;

    public DynamicTrafficDensity(ACServer server)
    {
        _server = server;
        _ = LoopAsync();
    }

    private async Task LoopAsync()
    {
        while (true)
        {
            try
            {
                double hours = _server.CurrentDateTime.TimeOfDay.TickOfDay / 10_000_000.0 / 3600.0;
                _server.Configuration.Extra.AiParams.TrafficDensity = GetDensity(hours);
                _server.Configuration.TriggerReload();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in dynamic traffic density update");
            }
            finally
            {
                await Task.Delay(TimeSpan.FromMinutes(10.0 / _server.Configuration.Server.TimeOfDayMultiplier));
            }
        }
    }

    private float GetDensity(double hourOfDay)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (Math.Truncate(hourOfDay) == hourOfDay)
        {
            return _server.Configuration.Extra.AiParams.HourlyTrafficDensity![(int)hourOfDay];
        }

        int lowerBound = (int)Math.Floor(hourOfDay);
        int higherBound = (int)Math.Ceiling(hourOfDay) % 24;

        return (float)MathUtils.Lerp(_server.Configuration.Extra.AiParams.HourlyTrafficDensity![lowerBound], _server.Configuration.Extra.AiParams.HourlyTrafficDensity![higherBound], hourOfDay - lowerBound);
    }
}
