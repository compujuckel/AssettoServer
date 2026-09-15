using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using DotNext.Runtime.InteropServices;

namespace AssettoServer.Utils;

public static class StreamExtensions
{
    /// <param name="stream">The stream to write into.</param>
    extension(Stream stream)
    {
        public void Write<T>(List<T> list)
            where T : unmanaged => stream.Write(CollectionsMarshal.AsSpan(list));
        
        public void Write<T>(T[] list)
            where T : unmanaged => stream.Write(list.AsSpan());

        public void Write<T>(Span<T> list)
            where T : unmanaged => stream.Write((ReadOnlySpan<T>)list);

        public void Write<T>(ReadOnlySpan<T> list)
            where T : unmanaged
        {
            stream.Write(MemoryMarshal.AsBytes(list));
        }

        /// Copied from DotNext 4.x, they removed it in 5.0 :(
        /// 
        /// <summary>
        /// Serializes value to the stream.
        /// </summary>
        /// <param name="value">The value to be written into the stream.</param>
        /// <typeparam name="T">The value type to be serialized.</typeparam>
        public void Write<T>(in T value)
            where T : unmanaged => stream.Write(MemoryMarshal.AsReadOnlyBytes(in value));
    }
}
