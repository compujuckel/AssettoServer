namespace AssettoServer.Shared.Model;

public class EntryCarResult
{
    public uint BestLap { get; set; } = 999999999;
    public uint NumLaps { get; set; } = 0;
    public uint TotalTime { get; set; } = 0;
    public uint LastLap { get; set; } = 999999999;
    public bool HasCompletedLastLap { get; set; } = false;
}
