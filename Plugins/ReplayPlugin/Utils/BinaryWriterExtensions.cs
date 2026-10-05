using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using JetBrains.Annotations;

namespace ReplayPlugin.Utils;

public static class BinaryWriterExtensions
{
    public static void WriteStruct<T>(this BinaryWriter writer, in T value) where T : unmanaged
    {
        writer.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in value, 1)));
    }

    public static void WriteACString(this BinaryWriter writer, string? str)
    {
        str ??= "";
        writer.Write((uint)str.Length);
        writer.Write(Encoding.UTF8.GetBytes(str));
    }

    public static void WriteLengthPrefixed(this BinaryWriter writer, ReadOnlySpan<byte> data)
    {
        writer.Write((uint)data.Length);
        writer.Write(data);
    }

    public static void WriteCspCompressedExtraData(this BinaryWriter writer, ulong id, [InstantHandle] Action<Stream> writeAction)
    {
        var lengthPosition = writer.BaseStream.Position;
        writer.Write((uint)0);

        writer.Write(id);
        
        using (var zlibStream = new ZLibStream(writer.BaseStream, CompressionMode.Compress, true))
        {
            writeAction(zlibStream);
        }
        
        var afterPosition = writer.BaseStream.Position;
        var length = (int)(afterPosition - lengthPosition - 4 /* length */);
        
        writer.Seek((int)lengthPosition, SeekOrigin.Begin);
        writer.Write(length);
        writer.Seek((int)afterPosition, SeekOrigin.Begin);
    }
}
