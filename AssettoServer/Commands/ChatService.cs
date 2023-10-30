﻿using System;
using System.Reflection;
using System.Threading.Tasks;
using AssettoServer.Commands.Contexts;
using AssettoServer.Commands.TypeParsers;
using AssettoServer.Network.Rcon;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Network.Packets.Shared;
using Qmmands;
using Serilog;

namespace AssettoServer.Commands;

public class ChatService
{
    private readonly EntryCarManager _entryCarManager;
    private readonly Func<ACTcpClient, ChatCommandContext> _chatContextFactory;
    private readonly Func<RconClient, int, RconCommandContext> _rconContextFactory;
    private readonly CommandService _commandService = new(new CommandServiceConfiguration
    {
        DefaultRunMode = RunMode.Parallel
    });

    public event EventHandler<ACTcpClient, ChatEventArgs>? MessageReceived;

    public ChatService(ACPluginLoader loader, Func<ACTcpClient, ChatCommandContext> chatContextFactory, ACClientTypeParser acClientTypeParser, EntryCarManager entryCarManager, Func<RconClient, int, RconCommandContext> rconContextFactory)
    {
        _chatContextFactory = chatContextFactory;
        _entryCarManager = entryCarManager;
        _rconContextFactory = rconContextFactory;
        _entryCarManager.ClientConnected += OnClientConnected;

        _commandService.AddModules(Assembly.GetEntryAssembly());
        _commandService.AddTypeParser(acClientTypeParser);
        _commandService.CommandExecutionFailed += OnCommandExecutionFailed;
        _commandService.CommandExecuted += OnCommandExecuted;

        foreach (var plugin in loader.LoadedPlugins)
        { 
            _commandService.AddModules(plugin.Assembly);
        }
    }

    private void OnClientConnected(ACTcpClient sender, EventArgs args)
    {
        sender.ChatMessageReceived += OnChatMessageReceived;
    }

    private static ValueTask OnCommandExecuted(object? sender, CommandExecutedEventArgs args)
    {
        if (args.Context is RconCommandContext context)
        {
            context.SendRconResponse();
        }

        return ValueTask.CompletedTask;
    }

    private async Task ProcessCommandAsync(ACTcpClient client, ChatMessage message)
    {
        var context = _chatContextFactory(client);
        var result = await _commandService.ExecuteAsync(message.Message, context);

        if (result is ChecksFailedResult checksFailedResult)
            context.Reply(checksFailedResult.FailedChecks[0].Result.FailureReason);
        else if (result is FailedResult failedResult)
            context.Reply(failedResult.FailureReason);
    }

    public async Task ProcessCommandAsync(RconClient client, int requestId, string command)
    {
        var context = _rconContextFactory(client, requestId);
        var result = await _commandService.ExecuteAsync(command, context);

        if (result is ChecksFailedResult checksFailedResult)
        {
            context.Reply(checksFailedResult.FailedChecks[0].Result.FailureReason);
            context.SendRconResponse();
        }
        else if (result is FailedResult failedResult)
        {
            context.Reply(failedResult.FailureReason);
            context.SendRconResponse();
        }
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
