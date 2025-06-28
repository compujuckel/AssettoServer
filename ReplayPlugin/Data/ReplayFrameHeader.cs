namespace ReplayPlugin.Data;

public struct ReplayFrameHeader
{
    public long ServerTime;
    public Half SunAngle;
    public byte CarFrameCount;
    public ushort AiFrameCount;
    public ushort AiMappingCount;
    public uint PlayerInfoIndex;
}
