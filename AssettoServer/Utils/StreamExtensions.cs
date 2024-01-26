using System.IO;
using DotNext;

namespace AssettoServer.Utils;

public static class StreamExtensions
{
    /// Copied from DotNext 4.x, they removed it in 5.0 :(
    /// 
    /// <summary>
    /// Serializes value to the stream.
    /// </summary>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The value to be written into the stream.</param>
    /// <typeparam name="T">The value type to be serialized.</typeparam>
    public static void Write<T>(this Stream stream, in T value)
        where T : unmanaged => stream.Write(Span.AsReadOnlyBytes(in value));
}
