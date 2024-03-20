using AssettoServer.Commands;
using AssettoServer.Commands.Contexts;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Qmmands;
using Serilog;

namespace CustomCommandPlugin;

public class CustomCommand : CriticalBackgroundService, IAssettoServerAutostart
{
    public CustomCommand(CustomCommandConfiguration configuration, CommandService commandService, IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        commandService.AddModule<ACModuleBase>(m =>
        {
            foreach (var (alias, response) in configuration.Commands)
            {
                if (!commandService.FindCommands(alias).Any(c => c.Command.Aliases.Any(a => a == alias)))
                {
                    m.AddCommand(ctx =>
                    {
                        Callback((BaseCommandContext) ctx);
                    }, c =>
                    {
                        c.Aliases.Add(alias);
                    });

                    void Callback(BaseCommandContext c)
                    {
                        c.Reply(response);
                    }
                }
                else
                {
                    Log.Warning("Command {Alias} does already exist, please use something else", alias);
                }
            }
        });
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Debug("CustomCommandPlugin autostart called");
        return Task.CompletedTask;
    }
}
