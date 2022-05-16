using System.Text.RegularExpressions;
using AssettoServer.Commands;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;

namespace WordFilterPlugin;

public class WordFilter
{
    private readonly EntryCarManager _entryCarManager;
    private readonly WordFilterConfiguration _configuration;

    public WordFilter(WordFilterConfiguration configuration, EntryCarManager entryCarManager, ChatService chatService)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;

        _entryCarManager.ClientConnecting += OnClientConnecting;
        chatService.MessageReceived += OnChatMessageReceived;
    }

    private void OnClientConnecting(ACTcpClient sender, ClientConnectingEventArgs args)
    {
        if (_configuration.ProhibitedUsernamePatterns.Any(regex => Regex.Match(args.HandshakeRequest.Name, regex, RegexOptions.IgnoreCase).Success))
        {
            args.Cancel = true;
            args.CancelType = ClientConnectingEventArgs.CancelTypeEnum.AuthFailed;
            args.AuthFailedReason = "Prohibited username. Change your Online Name in Settings > Content Manager > Drive > Online Name.";
        }
    }

    private void OnChatMessageReceived(ACTcpClient sender, ChatEventArgs args)
    {
        if (_configuration.BannableChatPatterns.Any(regex => Regex.Match(args.Message, regex, RegexOptions.IgnoreCase).Success))
        {
            args.Cancel = true;
            sender.Logger.Information("Chat message from {ClientName} ({SessionId}) filtered and banned: {ChatMessage}", sender.Name, sender.SessionId, args.Message);
            Task.Run(() => _entryCarManager.BanAsync(sender, "prohibited language"));
        }
        else if (_configuration.ProhibitedChatPatterns.Any(regex => Regex.Match(args.Message, regex, RegexOptions.IgnoreCase).Success))
        {
            args.Cancel = true;
            sender.Logger.Information("Chat message from {ClientName} ({SessionId}) filtered: {ChatMessage}", sender.Name, sender.SessionId, args.Message);
        }
    }
}
