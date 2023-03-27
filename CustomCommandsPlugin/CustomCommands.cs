using AssettoServer.Server.Plugin;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace CustomCommandsPlugin;

public class CustomCommands : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly CustomCommandsConfiguration _configuration;

    public CustomCommands(CustomCommandsConfiguration configuration, IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _configuration = configuration;
        Log.Debug($"CustomCommands plugin enabled. Discord link: [{_configuration.DiscordURL}]");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Debug("CustomCommands plugin autostart called");
        return Task.CompletedTask;
    }
}
