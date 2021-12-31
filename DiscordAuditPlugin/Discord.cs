using System.Drawing;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using CSharpDiscordWebhook.NET.Discord;
using Serilog;

namespace DiscordAuditPlugin;

public class Discord
{
    private static readonly string[] SensitiveCharacters = { "\\", "*", "_", "~", "`", "|", ">", ":", "@" };
    private readonly ACServer _server;

    private DiscordWebhook AuditHook { get; }
    private DiscordWebhook ChatHook { get; }
    private DiscordConfiguration Configuration { get; }

    public Discord(ACServer server, DiscordConfiguration configuration)
    {
        _server = server;
        Configuration = configuration;

        if (!string.IsNullOrEmpty(Configuration.AuditUrl))
        {
            AuditHook = new DiscordWebhook
            {
                Uri = new Uri(Configuration.AuditUrl)
            };

            server.ClientKicked += OnClientKicked;
            server.ClientBanned += OnClientBanned;
        }

        if (!string.IsNullOrEmpty(Configuration.ChatUrl))
        {
            ChatHook = new DiscordWebhook
            {
                Uri = new Uri(Configuration.ChatUrl)
            };

            server.ChatMessageReceived += OnChatMessageReceived;
        }
    }

    private void OnClientBanned(ACTcpClient sender, ClientAuditEventArgs args)
    {
        AuditHook.SendAsync(PrepareAuditMessage(
            ":hammer: Ban alert",
            _server.Configuration.Name,
            sender.Guid,
            sender.Name,
            args.ReasonStr,
            Color.Red,
            args.Admin?.Name
        )).ContinueWith(t => Log.Error(t.Exception, "Error in Discord webhook"), TaskContinuationOptions.OnlyOnFaulted);
    }

    private void OnClientKicked(ACTcpClient sender, ClientAuditEventArgs args)
    {
        if (args.Reason != KickReason.ChecksumFailed)
        {
            AuditHook.SendAsync(PrepareAuditMessage(
                ":boot: Kick alert",
                _server.Configuration.Name,
                sender.Guid,
                sender.Name,
                args.ReasonStr,
                Color.Yellow,
                args.Admin?.Name
            )).ContinueWith(t => Log.Error(t.Exception, "Error in Discord webhook"), TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    private void OnChatMessageReceived(ACTcpClient sender, ChatEventArgs args)
    {
        if (!args.Message.StartsWith("\t\t\t\t$CSP0:") && !string.IsNullOrWhiteSpace(args.Message))
        {
            DiscordMessage msg = new DiscordMessage
            {
                AvatarUrl = Configuration.PictureUrl,
                Username = _server.Configuration.Name,
                Content = Sanitize($"{sender.Name}: {args.Message}")
            };

            ChatHook.SendAsync(msg)
                .ContinueWith(t => Log.Error(t.Exception, "Error in Discord webhook"), TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    private DiscordMessage PrepareAuditMessage(
        string title,
        string serverName,
        string clientGuid,
        string clientName,
        string reason,
        Color color,
        string adminName
    )
    {
        string userSteamUrl = "https://steamcommunity.com/profiles/" + clientGuid;
        DiscordMessage message = new DiscordMessage
        {
            Username = serverName.Substring(0, Math.Min(serverName.Length, 80)),
            AvatarUrl = Configuration.PictureUrl,
            Embeds = new List<DiscordEmbed>
            {
                new()
                {
                    Title = title,
                    Color = color,
                    Fields = new List<EmbedField>
                    {
                        new() { Name = "Name", Value = Sanitize(clientName), InLine = true },
                        new() { Name = "Steam-GUID", Value = clientGuid + " ([link](" + userSteamUrl + "))", InLine = true }
                    }
                }
            }
        };

        if (adminName != null)
            message.Embeds[0].Fields.Add(new EmbedField { Name = "By Admin", Value = Sanitize(adminName), InLine = true });

        if (reason != null)
            message.Embeds[0].Fields.Add(new EmbedField { Name = "Message", Value = Sanitize(reason) });

        return message;
    }

    private static string Sanitize(string text)
    {
        foreach (string unsafeChar in SensitiveCharacters)
            text = text.Replace(unsafeChar, $"\\{unsafeChar}");
        return text;
    }
}