using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Tasks;
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Utils;
using DotNext.Buffers;
using Microsoft.AspNetCore.Connections;

namespace AssettoServer.Network.Tcp;

public class TcpConnectionMiddleware
{
    private readonly ConnectionDelegate _next;
    private readonly Func<Stream, IPEndPoint, ACTcpClient> _acTcpClientFactory;

    public TcpConnectionMiddleware(ConnectionDelegate next, Func<Stream, IPEndPoint, ACTcpClient> acTcpClientFactory)
    {
        _next = next;
        _acTcpClientFactory = acTcpClientFactory;
    }

    public async Task OnConnectionAsync(ConnectionContext context)
    {
        if (await IsAssettoProtocolAsync(context.Transport.Input))
        {
            ACTcpClient acClient = _acTcpClientFactory(DuplexPipeStreamFactory.Create(context.Transport.Input, context.Transport.Output), (IPEndPoint)context.RemoteEndPoint!);
            await acClient.RunAsync();
        }
        else
        {
            await _next(context);
        }
    }

    private static async Task<bool> IsAssettoProtocolAsync(PipeReader pipe)
    {
        var result = await pipe.ReadAtLeastAsync(3 /* packet size + type */);

        var reader = new SequenceReader(result.Buffer);
        reader.Skip(2);
        var firstByte = reader.ReadByte();
        
        pipe.AdvanceTo(result.Buffer.Start);
        
        return firstByte == (byte)ACServerProtocol.RequestNewConnection;
    }
}
