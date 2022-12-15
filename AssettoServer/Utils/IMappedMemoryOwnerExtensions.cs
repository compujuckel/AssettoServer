using System.Diagnostics.CodeAnalysis;
using DotNext.IO.MemoryMappedFiles;

namespace AssettoServer.Utils;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class IMappedMemoryOwnerExtensions
{
    public static void Prefault(this IMappedMemoryOwner self)
    {
        var bytes = self.Bytes;
        for (int i = 0; i < bytes.Length; i += 1024)
        {
            var x = bytes[i];
        }
    }
}
