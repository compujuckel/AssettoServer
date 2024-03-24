namespace ReplayPlugin.Data;

public class ReplayFrame
{
    public long ServerTime;
    public Half SunAngle;
    
    public Memory<ReplayCarFrame> CarFrames;
    public Memory<ReplayCarFrame> AiFrames;

    public Dictionary<byte, List<ushort>> AiFrameMapping = new();
}
