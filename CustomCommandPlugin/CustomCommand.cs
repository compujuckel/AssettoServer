using AssettoServer.Commands;
using AssettoServer.Commands.Contexts;
using Microsoft.Extensions.Hosting;
using Qmmands;
using Serilog;

namespace CustomCommandPlugin;

public class CustomCommand : IHostedService
{
    private readonly CustomCommandConfiguration _configuration;
    private readonly CommandService _commandService;

    public CustomCommand(CustomCommandConfiguration configuration, CommandService commandService)
    {
        _configuration = configuration;
        _commandService = commandService;
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        _commandService.AddModule<ACModuleBase>(m =>
        {
            foreach (var (alias, response) in _configuration.Commands)
            {
                if (!_commandService.FindCommands(alias).Any(c => c.Command.Aliases.Any(a => a == alias)))
                {
                    m.AddCommand(ctx =>
                    {
                        ((BaseCommandContext) ctx).Reply(response);
                    }, c =>
                    {
                        c.Aliases.Add(alias);
                    });
                }
                else
                {
                    Log.Warning("Command {Alias} does already exist, please use something else", alias);
                }
            }
        });
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
