namespace AssettoServer.Shared.Utils;

public static class MemoryStreamExtensions
{
    public static Span<byte> GetSpan(this MemoryStream stream)
    {
        return stream.GetBuffer().AsSpan(0, (int)stream.Length);
    }
    
    public static Memory<byte> GetMemory(this MemoryStream stream)
    {
        return stream.GetBuffer().AsMemory(0, (int)stream.Length);
    }
}
