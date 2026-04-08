namespace ReplayPlugin;

public class ReplayWriterJob
{
    public string Path { get; }
    public long StartTime { get; }
    public long EndTime { get; }
    public byte TargetSessionId { get; }
    
    public readonly TaskCompletionSource TaskCompletionSource = new();
    
    public ReplayWriterJob(string path, long startTime, long endTime, byte targetSessionId)
    {
        Path = path;
        StartTime = startTime;
        EndTime = endTime;
        TargetSessionId = targetSessionId;
    }
}
