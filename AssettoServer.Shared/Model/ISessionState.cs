namespace AssettoServer.Shared.Model;

public interface ISessionState
{
    public ISession Configuration { get; }
    public int EndTime { get; set; }
    public long StartTimeMilliseconds { get; }
    public int TimeLeftMilliseconds { get; }
    public int SessionTimeMilliseconds { get; }
    public uint TargetLap { get; set; }
    public uint LeaderLapCount { get; set; }
    public bool LeaderHasCompletedLastLap { get; set; }
    public bool SessionOverFlag { get; set; }
    public Dictionary<byte, EntryCarResult>? Results { get; set; }
    public IEnumerable<IEntryCar<IClient>>? Grid { get; set; }
}
