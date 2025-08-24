using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Utils;
using DotNext.IO;
using Microsoft.AspNetCore.Connections;
using Serilog;

namespace AssettoServer.Network.Tcp;

public class TcpConnectionMiddleware
{
    private readonly ConnectionDelegate _next;
    private readonly Func<Stream, IPEndPoint, ACTcpClient> _acTcpClientFactory;

    public TcpConnectionMiddleware(ConnectionDelegate next, Func<Stream, IPEndPoint, ACTcpClient> acTcpClientFactory)
    {
        Log.Debug("Created TcpConnectionMiddleware");
        _next = next;
        _acTcpClientFactory = acTcpClientFactory;
    }

    public async Task OnConnectionAsync(ConnectionContext context)
    {
        var pipe = context.Transport.Input;
        var result = await pipe.ReadAtLeastAsync(3 /* packet size + type */);

        var reader = new SequenceReader(result.Buffer);
        reader.Skip(2);
        var firstByte = reader.ReadByte();
        
        pipe.AdvanceTo(result.Buffer.Start);
        
        if (firstByte == (byte)ACServerProtocol.RequestNewConnection)
        {
            ACTcpClient acClient = _acTcpClientFactory(DuplexPipeStreamFactory.Create(context.Transport.Input, context.Transport.Output), (IPEndPoint)context.RemoteEndPoint!);
            await acClient.RunAsync();
        }
        else
        {
            await _next(context);
        }
    }
}
