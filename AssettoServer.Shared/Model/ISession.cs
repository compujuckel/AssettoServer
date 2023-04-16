namespace AssettoServer.Shared.Model;

public interface ISession
{
    public int Id { get; }
    public SessionType Type { get; }
    public string Name { get; }
    public uint Time { get; }
    public uint Laps { get; }
    public uint WaitTime { get; }
    public bool IsOpen { get; }
    public bool IsTimedRace => Time > 0 && Laps == 0;
}
