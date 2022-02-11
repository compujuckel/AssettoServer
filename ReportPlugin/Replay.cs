namespace ReportPlugin;

public class Replay
{
    public Guid Guid { get; }
    public AuditLog AuditLog { get; }
    public bool Submitted { get; set; }

    public Replay(Guid guid, AuditLog auditLog)
    {
        Guid = guid;
        AuditLog = auditLog;
    }
}
