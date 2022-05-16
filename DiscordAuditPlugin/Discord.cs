using System.Drawing;
using AssettoServer.Commands;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using CSharpDiscordWebhook.NET.Discord;
using Serilog;

namespace DiscordAuditPlugin;

public class Discord
{
    private static readonly string[] SensitiveCharacters = { "\\", "*", "_", "~", "`", "|", ">", ":", "@" };
    private readonly string _serverNameTruncated;

    private readonly DiscordConfiguration _configuration;

    private DiscordWebhook? AuditHook { get; }
    private DiscordWebhook? ChatHook { get; }

    public Discord(DiscordConfiguration configuration, EntryCarManager entryCarManager, ACServerConfiguration serverConfiguration, ChatService chatService)
    {
        _serverNameTruncated = serverConfiguration.Server.Name.Substring(0, Math.Min(serverConfiguration.Server.Name.Length, 80));
        _configuration = configuration;

        if (!string.IsNullOrEmpty(_configuration.AuditUrl))
        {
            AuditHook = new DiscordWebhook
            {
                Uri = new Uri(_configuration.AuditUrl)
            };

            entryCarManager.ClientKicked += OnClientKicked;
            entryCarManager.ClientBanned += OnClientBanned;
        }
        
        if (!string.IsNullOrEmpty(_configuration.ChatUrl))
        {
            ChatHook = new DiscordWebhook
            {
                Uri = new Uri(_configuration.ChatUrl)
            };

            chatService.MessageReceived += OnChatMessageReceived;
        }
    }

    private void OnClientBanned(ACTcpClient sender, ClientAuditEventArgs args)
    {
        Task.Run(async () =>
        {
            try
            {
                await AuditHook!.SendAsync(PrepareAuditMessage(
                    ":hammer: Ban alert",
                    _serverNameTruncated,
                    sender.Guid, 
                    sender.Name,
                    args.ReasonStr,
                    Color.Red,
                    args.Admin?.Name
                ));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in Discord webhook");
            }
        });
    }

    private void OnClientKicked(ACTcpClient sender, ClientAuditEventArgs args)
    {
        if (args.Reason != KickReason.ChecksumFailed)
        {
            Task.Run(async () =>
            {
                try
                {
                    await AuditHook!.SendAsync(PrepareAuditMessage(
                        ":boot: Kick alert",
                        _serverNameTruncated,
                        sender.Guid, 
                        sender.Name,
                        args.ReasonStr,
                        Color.Yellow,
                        args.Admin?.Name
                    ));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in Discord webhook");
                }
            });
        }
    }

    private void OnChatMessageReceived(ACTcpClient sender, ChatEventArgs args)
    {
        if (!args.Message.StartsWith("\t\t\t\t$CSP0:") && !string.IsNullOrWhiteSpace(args.Message))
        {
            string username;
            string content;

            if (_configuration.ChatMessageIncludeServerName)
            {
                username = _serverNameTruncated;
                content = $"**{sender.Name}:** {Sanitize(args.Message)}";
            }
            else
            {
                username = sender.Name ?? throw new InvalidOperationException("ACTcpClient has no name set");
                content = Sanitize(args.Message);
            }

            DiscordMessage msg = new DiscordMessage
            {
                AvatarUrl = _configuration.PictureUrl,
                Username = username,
                Content = content,
                AllowedMentions = new AllowedMentions()
            };

            ChatHook!.SendAsync(msg)
                .ContinueWith(t => Log.Error(t.Exception, "Error in Discord webhook"), TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    private DiscordMessage PrepareAuditMessage(
        string title,
        string serverName,
        string? clientGuid,
        string? clientName,
        string? reason,
        Color color,
        string? adminName
    )
    {
        string userSteamUrl = "https://steamcommunity.com/profiles/" + clientGuid;
        DiscordMessage message = new DiscordMessage
        {
            Username = serverName.Substring(0, Math.Min(serverName.Length, 80)),
            AvatarUrl = _configuration.PictureUrl,
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
            },
            AllowedMentions = new AllowedMentions()
        };

        if (adminName != null)
            message.Embeds[0].Fields.Add(new EmbedField { Name = "By Admin", Value = Sanitize(adminName), InLine = true });

        if (reason != null)
            message.Embeds[0].Fields.Add(new EmbedField { Name = "Message", Value = Sanitize(reason) });

        return message;
    }

    private static string Sanitize(string? text)
    {
        text ??= "";
        
        foreach (string unsafeChar in SensitiveCharacters)
            text = text.Replace(unsafeChar, $"\\{unsafeChar}");
        return text;
    }
}
