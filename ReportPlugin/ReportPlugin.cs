using System.Collections.Concurrent;
using System.Drawing;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using CSharpDiscordWebhook.NET.Discord;
using Serilog;

namespace ReportPlugin;

internal class ReportPlugin
{
    private static readonly string[] SensitiveCharacters = { "\\", "*", "_", "~", "`", "|", ">", ":", "@" };
    
    internal ACServer Server { get; }
    internal Guid Key { get; }
    internal Dictionary<ACTcpClient, Replay> Reports { get; } = new();

    private readonly ReportConfiguration _configuration;
    private readonly ConcurrentQueue<AuditEvent> _events = new();
    private readonly DiscordWebhook? _webhook;
    private readonly string _serverNameTruncated;

    internal ReportPlugin(ACServer server, ReportConfiguration configuration)
    {
        Server = server;
        _configuration = configuration;

        Server.ClientFirstUpdateSent += OnClientFirstUpdateSent;
        Server.ClientDisconnected += OnClientDisconnected;
        Server.ChatMessageReceived += OnChatMessage;
        
        _serverNameTruncated = server.Configuration.Server.Name.Substring(0, Math.Min(server.Configuration.Server.Name.Length, 80));

        if (!string.IsNullOrEmpty(_configuration.WebhookUrl))
        {
            _webhook = new DiscordWebhook
            {
                Uri = new Uri(_configuration.WebhookUrl)
            };
        }
        
        Key = Guid.NewGuid();
        string extraOptions = $"\n[REPLAY_CLIPS]\nUPLOAD_URL = 'http://{Server.GeoParams.Ip}:{Server.Configuration.Server.HttpPort}/report?key={Key}'\nDURATION = {_configuration.ClipDurationSeconds}";
        Server.CSPServerExtraOptions.ExtraOptions += extraOptions;

        Directory.CreateDirectory("reports");
    }

    private void OnClientFirstUpdateSent(ACTcpClient sender, EventArgs args)
    {
        try
        {
            var auditEvent = new PlayerConnectedAuditEvent(new AuditClient(sender.EntryCar));
            _events.Enqueue(auditEvent);
            DeleteOldEvents();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error enqueueing audit event");
        }
    }

    private void OnClientDisconnected(ACTcpClient sender, EventArgs args)
    {
        try
        {
            var auditEvent = new PlayerDisconnectedAuditEvent(new AuditClient(sender));
            _events.Enqueue(auditEvent);
            DeleteOldEvents();

            Reports.Remove(sender);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error enqueueing audit event");
        }
    }

    private void OnChatMessage(ACTcpClient sender, ChatEventArgs args)
    {
        try
        {
            var auditEvent = new ChatMessageAuditEvent(new AuditClient(sender), args.Message);
            _events.Enqueue(auditEvent);
            DeleteOldEvents();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error enqueueing audit event");
        }
    }

    private void DeleteOldEvents()
    {
        while (_events.TryPeek(out var auditEvent))
        {
            if (auditEvent.Timestamp < DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(_configuration.ClipDurationSeconds)))
            {
                _events.TryDequeue(out _);
            }
            else
            {
                break;
            }
        }
    }
    
    private static string Sanitize(string? text)
    {
        text ??= "";
        
        foreach (string unsafeChar in SensitiveCharacters)
            text = text.Replace(unsafeChar, $"\\{unsafeChar}");
        return text;
    }

    internal AuditLog GetAuditLog(DateTime timestamp)
    {
        DeleteOldEvents();
        var entryList = Server.EntryCars.Select(car => new AuditClient(car));
        return new AuditLog(timestamp, entryList, _events.ToList());
    }

    internal async Task SubmitReport(ACTcpClient client, Replay replay, string reason)
    {
        if (_webhook == null)
            return;
        
        var msg = new DiscordMessage
        {
            Username = _serverNameTruncated,
            Embeds = new List<DiscordEmbed>
            {
                new DiscordEmbed
                {
                    Author = new EmbedAuthor
                    {
                        Name = Sanitize(client.Name),
                        Url = $"https://steamcommunity.com/profiles/{client.Guid}"
                    },
                    Color = Color.Red,
                    Description = Sanitize(reason),
                    Footer = new EmbedFooter
                    {
                        Text = "AssettoServer"
                    },
                    Timestamp = replay.AuditLog.Timestamp,
                    Title = "Report received"
                }
            },
            AllowedMentions = new AllowedMentions()
        };

        await _webhook.SendAsync(msg, new FileInfo(Path.Join("reports", $"{replay.Guid}.zip")), new FileInfo(Path.Join("reports", $"{replay.Guid}.json")));
    }
}
