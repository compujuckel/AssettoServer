using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Configuration;
using Discord;
using Discord.Webhook;
using Serilog;

namespace AssettoServer.Server
{
    public class Discord
    {
        private static readonly string[] SensitiveCharacters = {"\\", "*", "_", "~", "`", "|", ">", ":"};
        private string PictureUrl { get; }
        private bool IsEnabled { get; }
        private DiscordWebhook AuditHook { get; }
        private DiscordWebhook ChatHook { get; }


        public Discord(ACExtraConfiguration extraConfiguration)
        {
            IsEnabled = extraConfiguration.UseDiscordWebHook;
            PictureUrl = extraConfiguration.DiscordWebHookPictureUrl;

            AuditHook = new DiscordWebhook
            {
                Url = extraConfiguration.DiscordWebHookAuditUrl
            };

            ChatHook = new DiscordWebhook
            {
                Url = extraConfiguration.DiscordWebHookChatUrl
            };
        }

        public void SendAuditKickMessage(string serverName, ACTcpClient client, string reason, ACTcpClient admin = null)
        {
            Run(() =>
            {
                if (IsEnabled && AuditHook.Url != null)
                {
                    AuditHook.Send(PrepareAuditMessage(
                        ":boot: Kick alert",
                        serverName,
                        client.Guid,
                        client.Name,
                        reason,
                        Color.Yellow,
                        admin?.Name
                    ));
                }
            });
        }

        public void SendAuditBanMessage(string serverName, ACTcpClient bannedClient, string reason, ACTcpClient admin = null)
        {
            Run(() =>
            {
                if (IsEnabled && AuditHook.Url != null)
                {
                    AuditHook.Send(PrepareAuditMessage(
                        ":hammer: Ban alert",
                        serverName,
                        bannedClient.Guid,
                        bannedClient.Name,
                        reason,
                        Color.Red,
                        admin?.Name
                    ));
                }
            });
        }

        public void SendChatMessage(string userName, string messageContent)
        {
            Run(() =>
            {
                if (!IsEnabled || ChatHook.Url == null)
                {
                    DiscordMessage message = new DiscordMessage
                    {
                        AvatarUrl = PictureUrl,
                        Username = userName,
                        Content = Sanitize(messageContent)
                    };

                    ChatHook.Send(message);
                }
            });
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
                Username = serverName,
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