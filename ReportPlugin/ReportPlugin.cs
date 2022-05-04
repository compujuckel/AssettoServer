using System.Collections.Concurrent;
using System.Drawing;
using AssettoServer.Commands;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.GeoParams;
using CSharpDiscordWebhook.NET.Discord;
using Serilog;

namespace ReportPlugin;

public class ReportPlugin
{
    private static readonly string[] SensitiveCharacters = { "\\", "*", "_", "~", "`", "|", ">", ":", "@" };
    
    internal Guid Key { get; }
    
    private readonly ReportConfiguration _configuration;
    private readonly DiscordWebhook? _webhook;
    private readonly string _serverNameTruncated;
    private readonly EntryCarManager _entryCarManager;
    private readonly Dictionary<ACTcpClient, Replay> _reports = new();
    private readonly ConcurrentQueue<AuditEvent> _events = new();

    internal ReportPlugin(ReportConfiguration configuration, EntryCarManager entryCarManager, ChatService chatService, CSPServerExtraOptions cspServerExtraOptions, ACServerConfiguration serverConfiguration, GeoParamsManager geoParamsManager)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;

        _entryCarManager.ClientConnected += (sender, _) =>  sender.FirstUpdateSent += OnClientFirstUpdateSent;
        _entryCarManager.ClientDisconnected += OnClientDisconnected;
        chatService.MessageReceived += OnChatMessage;
        
        _serverNameTruncated = serverConfiguration.Server.Name.Substring(0, Math.Min(serverConfiguration.Server.Name.Length, 80));

        if (!string.IsNullOrEmpty(_configuration.WebhookUrl))
        {
            _webhook = new DiscordWebhook
            {
                Uri = new Uri(_configuration.WebhookUrl)
            };
        }
        
        Key = Guid.NewGuid();
        string extraOptions = $"\n[REPLAY_CLIPS]\nUPLOAD_URL = 'http://{geoParamsManager.GeoParams.Ip}:{serverConfiguration.Server.HttpPort}/report?key={Key}'\nDURATION = {_configuration.ClipDurationSeconds}";
        cspServerExtraOptions.ExtraOptions += extraOptions;

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

            _reports.Remove(sender);
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
        var entryList = _entryCarManager.EntryCars.Select(car => new AuditClient(car));
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
    
    public Replay? GetLastReplay(ACTcpClient client)
    {
        _reports.TryGetValue(client, out var report);
        return report;
    }

    public void SetLastReplay(ACTcpClient client, Replay replay) => _reports[client] = replay;
}
