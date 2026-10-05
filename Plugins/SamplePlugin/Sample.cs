using Microsoft.Extensions.Hosting;
using Serilog;

namespace SamplePlugin;

public class Sample : BackgroundService
{
    public Sample(SampleConfiguration configuration)
    {
        Log.Debug("Sample plugin constructor called! Hello: {Hello}", configuration.Hello);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Debug("Sample plugin autostart called");
        return Task.CompletedTask;
    }
}
