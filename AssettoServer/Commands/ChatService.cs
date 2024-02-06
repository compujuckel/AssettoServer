using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AssettoServer.Commands.Contexts;
using AssettoServer.Commands.TypeParsers;
using AssettoServer.Network.Rcon;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Network.Packets.Shared;
using AssettoServer.Utils;
using Qmmands;
using Serilog;

namespace AssettoServer.Commands;

public partial class ChatService
{
    private readonly EntryCarManager _entryCarManager;
    private readonly Func<ACTcpClient, ChatCommandContext> _chatContextFactory;
    private readonly CommandService _commandService;

    public event EventHandler<ACTcpClient, ChatEventArgs>? MessageReceived;

    public ChatService(ACPluginLoader loader,
        Func<ACTcpClient, ChatCommandContext> chatContextFactory,
        ACClientTypeParser acClientTypeParser,
        EntryCarManager entryCarManager,
        CommandService commandService)
    {
        _chatContextFactory = chatContextFactory;
        _entryCarManager = entryCarManager;
        _commandService = commandService;
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
        => await ProcessCommandAsync(_chatContextFactory(client), message.Message);

    public async Task ProcessCommandAsync(BaseCommandContext context, string command)
    {
        var result = await _commandService.ExecuteAsync(command, context);

        if (result is ChecksFailedResult checksFailedResult)
            context.Reply(checksFailedResult.FailedChecks[0].Result.FailureReason);
        else if (result is FailedResult failedResult)
            context.Reply(failedResult.FailureReason);
    }

    private ValueTask OnCommandExecutionFailed(object? sender, CommandExecutionFailedEventArgs e)
    {
        if (!e.Result.IsSuccessful)
        {
            (e.Context as BaseCommandContext)?.Reply("An error occurred while executing this command.");
            Log.Error(e.Result.Exception, "Command execution failed: {Reason}", e.Result.FailureReason);
        }

        return ValueTask.CompletedTask;
    }
    
    private void OnChatMessageReceived(ACTcpClient sender, ChatMessageEventArgs args)
    {
        if (!CommandUtilities.HasPrefix(args.ChatMessage.Message, '/', out string commandStr))
        {
            var outArgs = new ChatEventArgs(args.ChatMessage.Message);
            MessageReceived?.Invoke(sender, outArgs);

            if (outArgs.Cancel) return;
            
            var oldVersionMessage = new ChatMessage {
                Message = args.ChatMessage.Message,
                SessionId = args.ChatMessage.SessionId
            };
            oldVersionMessage.Message = EmoteRegex().Replace(oldVersionMessage.Message, "(emote)");
                
            foreach (var car in _entryCarManager.EntryCars)
            {
                if (car.Client is { HasSentFirstUpdate: true })
                {
                    car.Client?.SendPacket(car.Client?.CSPVersion < CSPVersion.V0_1_80_p389 ? oldVersionMessage : args.ChatMessage);
                }
            }
        }
        else
        {
            var message = args.ChatMessage;
            message.Message = commandStr;
            _ = ProcessCommandAsync(sender, message);
        }
    }

    [GeneratedRegex(@"(\p{Cs}){2}")]
    private static partial Regex EmoteRegex();
}
