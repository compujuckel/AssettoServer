using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace AssettoServer.Utils;

public static class DuplexPipeStreamFactory
{
    public static Stream Create(PipeReader input, PipeWriter output)
    {
        return (Stream) CreateInternal(input, output, false);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.DuplexPipeStream, Microsoft.AspNetCore.Server.Kestrel.Core")]
    private static extern object CreateInternal(PipeReader input, PipeWriter output, bool leaveOpen);
}
