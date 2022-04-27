using System;
using System.Reflection;
using System.Threading.Tasks;
using AssettoServer.Commands.TypeParsers;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using Qmmands;
using Serilog;

namespace AssettoServer.Commands;

public class ChatService
{
    private readonly EntryCarManager _entryCarManager;
    private readonly Func<ACTcpClient, ChatMessage, ACCommandContext> _contextFactory;
    private readonly CommandService _commandService = new(new CommandServiceConfiguration
    {
        DefaultRunMode = RunMode.Parallel
    });

    public event EventHandler<ACTcpClient, ChatEventArgs>? MessageReceived;

    public ChatService(ACTcpServer tcpServer, ACPluginLoader loader, Func<ACTcpClient, ChatMessage, ACCommandContext> contextFactory, ACClientTypeParser acClientTypeParser, EntryCarManager entryCarManager)
    {
        _contextFactory = contextFactory;
        _entryCarManager = entryCarManager;
        _entryCarManager.ClientConnected += OnClientConnected;

        _commandService.AddModules(Assembly.GetEntryAssembly());
        _commandService.AddTypeParser(acClientTypeParser);
        _commandService.CommandExecutionFailed += OnCommandExecutionFailed;

        foreach (var plugin in loader.LoadedPlugins)
        { 
            _commandService.AddModules(plugin.Assembly);
        }
    }

    private void OnClientConnected(ACTcpClient sender, EventArgs args)
    {
        sender.ChatMessageReceived += OnChatMessageReceived;
    }

    private async Task ProcessCommandAsync(ACTcpClient client, ChatMessage message)
    {
        ACCommandContext context = _contextFactory(client, message);
        IResult result = await _commandService.ExecuteAsync(message.Message, context);

        if (result is ChecksFailedResult checksFailedResult)
            context.Reply(checksFailedResult.FailedChecks[0].Result.FailureReason);
        else if (result is FailedResult failedResult)
            context.Reply(failedResult.FailureReason);
    }
    
    private Task OnCommandExecutionFailed(CommandExecutionFailedEventArgs e)
    {
        if (!e.Result.IsSuccessful)
        {
            (e.Context as ACCommandContext)?.Reply("An error occurred while executing this command.");
            Log.Error(e.Result.Exception, "Command execution failed: {Reason}", e.Result.FailureReason);
        }

        return Task.CompletedTask;
    }
    
    private void OnChatMessageReceived(ACTcpClient sender, ChatMessageEventArgs args)
    {
        if (!CommandUtilities.HasPrefix(args.ChatMessage.Message, '/', out string commandStr))
        {
            var outArgs = new ChatEventArgs(args.ChatMessage.Message);
            MessageReceived?.Invoke(sender, outArgs);

            if (!outArgs.Cancel)
            {
                _entryCarManager.BroadcastPacket(args.ChatMessage);
            }
        }
        else
        {
            var message = args.ChatMessage;
            message.Message = commandStr;
            _ = ProcessCommandAsync(sender, message);
        }
    }
}
