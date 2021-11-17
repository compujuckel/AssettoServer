using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Tcp;
using Discord;
using Discord.Webhook;
using Serilog;

namespace AssettoServer.Server
{
    public class Discord
    {
        private static readonly string[] SensitiveCharacters = {"\\", "*", "_", "~", "`", "|", ">", ":"};
        private string PictureUrl { get; }
        private DiscordWebhook AuditHook { get; }
        private DiscordWebhook ChatHook { get; }


        public Discord(ACServer server)
        {
            if (server.Configuration.Extra.UseDiscordWebHook)
            {
                PictureUrl = server.Configuration.Extra.DiscordWebHookPictureUrl;

                string auditUrl = server.Configuration.Extra.DiscordWebHookAuditUrl;
                if (!string.IsNullOrEmpty(auditUrl))
                {
                    AuditHook = new DiscordWebhook
                    {
                        Url = auditUrl
                    };

                    server.ClientKicked += OnClientKicked;
                    server.ClientBanned += OnClientBanned;
                }

                string chatUrl = server.Configuration.Extra.DiscordWebHookChatUrl;
                if (!string.IsNullOrEmpty(chatUrl))
                {
                    ChatHook = new DiscordWebhook
                    {
                        Url = chatUrl
                    };

                    server.ChatMessageReceived += OnChatMessageReceived;
                }
            }
        }

        private void OnClientBanned(ACServer sender, ACTcpClient client, KickReason reason, string reasonStr, ACTcpClient admin)
        {
            Run(() =>
            {
                AuditHook.Send(PrepareAuditMessage(
                        ":hammer: Ban alert",
                        sender.Configuration.Name,
                        client.Guid,
                        client.Name,
                        reasonStr,
                        Color.Red,
                        admin?.Name
                        ));
            });
        }

        private void OnClientKicked(ACServer sender, ACTcpClient client, KickReason reason, string reasonStr, ACTcpClient admin)
        {
            if (reason != KickReason.ChecksumFailed)
            {
                Run(() =>
                {
                    AuditHook.Send(PrepareAuditMessage(
                        ":boot: Kick alert",
                        sender.Configuration.Name,
                        client.Guid,
                        client.Name,
                        reasonStr,
                        Color.Yellow,
                        admin?.Name
                    ));
                });
            }
        }

        private void OnChatMessageReceived(ACServer sender, ACTcpClient client, string message)
        {
            if (!message.StartsWith("\t\t\t\t$CSP0:"))
            {
                Run(() =>
                {
                    DiscordMessage msg = new DiscordMessage
                    {
                        AvatarUrl = PictureUrl,
                        Username = client.Name,
                        Content = Sanitize(message)
                    };

                    ChatHook.Send(msg);
                });
            }
        }

        private static void Run(Action action)
        {
            Task.Run(action)
                .ContinueWith(t => Log.Error(t.Exception, "Error in Discord webhook"), TaskContinuationOptions.OnlyOnFaulted);
        }

        private DiscordMessage PrepareAuditMessage(
            string title,
            string serverName,
            string clientGuid,
            string clientName,
            string reason,
            Color color,
            string adminName = null
        )
        {
            string userSteamUrl = "https://steamcommunity.com/profiles/" + clientGuid;
            DiscordMessage message = new DiscordMessage
            {
                Username = serverName.Substring(0, Math.Min(serverName.Length, 80)),
                AvatarUrl = PictureUrl,
                Embeds = new List<DiscordEmbed>
                {
                    new()
                    {
                        Title = title,
                        Color = color,
                        Fields = new List<EmbedField>
                        {
                            new() {Name = "Name", Value = Sanitize(clientName), InLine = true},
                            new() {Name = "Steam-GUID", Value = clientGuid + " ([link](" + userSteamUrl + "))", InLine = true}
                        }
                    }
                }
            };

            if (adminName != null)
                message.Embeds[0].Fields.Add(new EmbedField {Name = "By Admin", Value = Sanitize(adminName), InLine = true});

            if (reason != null)
                message.Embeds[0].Fields.Add(new EmbedField {Name = "Message", Value = Sanitize(reason)});

            return message;
        }

        private static string Sanitize(string text)
        {
            foreach (string unsafeChar in SensitiveCharacters)
                text = text.Replace(unsafeChar, $"\\{unsafeChar}");
            return text;
        }
    }
}