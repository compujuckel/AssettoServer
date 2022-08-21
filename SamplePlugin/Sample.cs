using AssettoServer.Server.Plugin;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SamplePlugin;

public class Sample : CriticalBackgroundService, IAssettoServerAutostart
{
    public Sample(SampleConfiguration configuration, IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        Log.Debug("Sample plugin constructor called! Hello: {Hello}", configuration.Hello);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Debug("Sample plugin autostart called");
        return Task.CompletedTask;
    }
}
