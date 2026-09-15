namespace AssettoServer.Shared.Utils;

public static class MemoryStreamExtensions
{
    extension(MemoryStream stream)
    {
        public Span<byte> GetSpan()
        {
            return stream.GetBuffer().AsSpan(0, (int)stream.Length);
        }

        public Memory<byte> GetMemory()
        {
            return stream.GetBuffer().AsMemory(0, (int)stream.Length);
        }
    }
}
