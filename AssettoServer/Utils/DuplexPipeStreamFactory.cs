using System;
using System.IO;
using System.IO.Pipelines;
using System.Reflection;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace AssettoServer.Utils;

public static class DuplexPipeStreamFactory
{
    private static readonly Type DuplexPipeStreamType = typeof(HttpProtocols).Assembly.GetType("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.DuplexPipeStream")!;
    private static readonly ConstructorInvoker Constructor = ConstructorInvoker.Create(DuplexPipeStreamType.GetConstructor([typeof(PipeReader), typeof(PipeWriter), typeof(bool)])!);
    
    public static Stream Create(PipeReader input, PipeWriter output)
    {
        return (Stream) Constructor.Invoke([input, output, false]);
    } 
}
