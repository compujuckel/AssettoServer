using System.Diagnostics.CodeAnalysis;
using DotNext.IO.MemoryMappedFiles;

namespace AssettoServer.Utils;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class IMappedMemoryExtensions
{
    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    private static byte _dummy;
    
    extension(IMappedMemory self)
    {
        public void Prefault()
        {
            var bytes = self.Bytes;
            for (int i = 0; i < bytes.Length; i += 1024)
            {
                _dummy = bytes[i];
            }
        }
    }
}
