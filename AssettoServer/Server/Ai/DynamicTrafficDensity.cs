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
                float hours = (float)TimeZoneInfo.ConvertTimeFromUtc(_server.CurrentDateTime, _server.TimeZone).TimeOfDay.TotalHours;
                _server.Configuration.Extra.AiParams.TrafficDensity = GetDensity(hours);
                _server.Configuration.TriggerReload();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in dynamic traffic density update");
            }
            finally
            {
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }
    }

    private float GetDensity(float hourOfDay)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (MathF.Truncate(hourOfDay) == hourOfDay)
        {
            return _server.Configuration.Extra.AiParams.HourlyTrafficDensity![(int)hourOfDay];
        }

        int lowerBound = (int)MathF.Floor(hourOfDay);
        int higherBound = (int)MathF.Ceiling(hourOfDay);

        return (float)MathUtils.Lerp(_server.Configuration.Extra.AiParams.HourlyTrafficDensity![lowerBound], _server.Configuration.Extra.AiParams.HourlyTrafficDensity![higherBound], hourOfDay - lowerBound);
    }
}
