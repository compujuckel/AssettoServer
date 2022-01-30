using System.Text.RegularExpressions;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using Serilog;

namespace WordFilterPlugin;

public class WordFilter
{
    private readonly ACServer _server;
    private readonly WordFilterConfiguration _configuration;

    public WordFilter(ACServer server, WordFilterConfiguration configuration)
    {
        _server = server;
        _configuration = configuration;

        _server.ClientHandshakeStarted += OnClientHandshakeStarted;
        _server.ChatMessageReceived += OnChatMessageReceived;
    }

    private void OnClientHandshakeStarted(ACTcpClient sender, ClientHandshakeEventArgs args)
    {
        if (_configuration.ProhibitedUsernamePatterns.Any(regex => Regex.Match(args.HandshakeRequest.Name, regex, RegexOptions.IgnoreCase).Success))
        {
            args.Cancel = true;
            args.CancelType = ClientHandshakeEventArgs.CancelTypeEnum.AuthFailed;
            args.AuthFailedReason = "Prohibited username. Change your Online Name in Settings > Content Manager > Drive > Online Name.";
        }
    }

    private void OnChatMessageReceived(ACTcpClient sender, ChatEventArgs args)
    {
        if (_configuration.BannableChatPatterns.Any(regex => Regex.Match(args.Message, regex, RegexOptions.IgnoreCase).Success))
        {
            args.Cancel = true;
            sender.Logger.Information("Chat message from {ClientName} ({SessionId}) filtered and banned: {ChatMessage}", sender.Name, sender.SessionId, args.Message);
            _server.BanAsync(sender, KickReason.VoteBlacklisted, "Prohibited language");
        }
        else if (_configuration.ProhibitedChatPatterns.Any(regex => Regex.Match(args.Message, regex, RegexOptions.IgnoreCase).Success))
        {
            args.Cancel = true;
            sender.Logger.Information("Chat message from {ClientName} ({SessionId}) filtered: {ChatMessage}", sender.Name, sender.SessionId, args.Message);
        }
    }
}