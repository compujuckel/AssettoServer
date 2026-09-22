using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace AssettoServer.Utils;

public static class ProcessExtensions
{
    extension(Process process)
    {
        [SupportedOSPlatform("windows")]
        public int ParentProcessId
        {
            get
            {
                _ = process.TryGetParentProcessId(out var id);
                return id;
            }
        }
        
        [UnsafeAccessor(UnsafeAccessorKind.Method)]
        [SupportedOSPlatform("windows")]
        private extern bool TryGetParentProcessId(out int parentProcessId);
    }
}
