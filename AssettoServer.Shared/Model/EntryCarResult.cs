
namespace AssettoServer.Shared.Model;

public class EntryCarResult
{
    public ulong Guid { get; set; } = 0;
    public string Name { get; set; } = "";
    public string Team { get; set; } = "";
    public string NationCode { get; set; } = "";
    public uint BestLap { get; set; } = 999999999;
    public uint NumLaps { get; set; } = 0;
    public uint TotalTime { get; set; } = 0;
    public uint LastLap { get; set; } = 999999999;
    public bool HasCompletedLastLap { get; set; } = false;
}
