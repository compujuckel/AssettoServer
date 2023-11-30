using System;
using System.Runtime.InteropServices;

namespace AssettoServer.Vendor.CSPXxHash3;

internal static unsafe partial class CSPXxHash3
{
    [LibraryImport("csp_xxhash3", EntryPoint = "XXH3_64bits")]
    private static partial long Hash64Internal(void* data, UIntPtr len);

    /// <summary>
    /// Only use this for CSP online event key generation. Use System.IO.Hashing if you need XXH for anything else
    /// </summary>
    internal static long Hash64(ReadOnlySpan<byte> data)
    {
        fixed (byte* ptr = data)
        {
            return Hash64Internal(ptr, (UIntPtr)data.Length);
        }
    }
}
