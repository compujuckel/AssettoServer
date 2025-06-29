using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Shared.Model;
using JetBrains.Annotations;

namespace ReportPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class AuditLog
{
    public DateTime Timestamp { get; }
    public IEnumerable<AuditClient> EntryList { get; }
    public List<AuditEvent> Events { get; }

    public AuditLog(DateTime timestamp, IEnumerable<AuditClient> entryList, List<AuditEvent> events)
    {
        Timestamp = timestamp;
        EntryList = entryList;
        Events = events;
    }
}

[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public abstract class AuditEvent
{
    public DateTime Timestamp { get; }
    public abstract string EventType { get; }

    protected AuditEvent()
    {
        Timestamp = DateTime.UtcNow;
    }
}

[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public readonly struct AuditClient
{
    public readonly string Name;
    public readonly ulong SteamId;
    public readonly byte SessionId;
    public readonly string CarModel;
    public readonly string Skin;

    public AuditClient(ACTcpClient client)
    {
        Name = client.Name ?? "";
        SteamId = client.Guid;
        SessionId = client.SessionId;
        CarModel = client.EntryCar.Model;
        Skin = client.EntryCar.Skin;
    }

    public AuditClient(EntryCar car)
    {
        Name = car.Client?.Name ?? "";
        SteamId = car.Client?.Guid ?? 0;
        SessionId = car.SessionId;
        CarModel = car.Model;
        Skin = car.Skin;
    }
}

[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class PlayerConnectedAuditEvent : AuditEvent
{
    public override string EventType { get; } = "PlayerConnected";
    public AuditClient Client { get; }

    public PlayerConnectedAuditEvent(AuditClient client)
    {
        Client = client;
    }
}

[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class PlayerDisconnectedAuditEvent : AuditEvent
{
    public override string EventType { get; } = "PlayerDisconnected";
    public AuditClient Client { get; }

    public PlayerDisconnectedAuditEvent(AuditClient client)
    {
        Client = client;
    }
}

[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class ChatMessageAuditEvent : AuditEvent
{
    public override string EventType { get; } = "ChatMessage";
    public AuditClient Client { get; }
    public string Message { get; }

    public ChatMessageAuditEvent(AuditClient client, string message)
    {
        Client = client;
        Message = message;
    }
}
