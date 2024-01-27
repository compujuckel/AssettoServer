using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AssettoServer.Commands;
using AssettoServer.Commands.Contexts;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Shared.Network.Packets.Outgoing;
using Serilog;

namespace AssettoServer.Network.Rcon;

public class RconClient
{
    private readonly ACServerConfiguration _configuration;
    private readonly ChatService _chatService;
    
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly Channel<IOutgoingNetworkPacket> _outgoingPacketChannel = Channel.CreateBounded<IOutgoingNetworkPacket>(256);
    private readonly Memory<byte> _tcpSendBuffer = new byte[4096];
    private readonly CancellationTokenSource _disconnectTokenSource = new ();
    private readonly Func<RconClient, int, RconCommandContext> _rconContextFactory;

    private bool _isAuthenticated = false;
    private bool _isDisconnectRequested = false;

    private Task SendLoopTask { get; set; } = null!;
    
    public RconClient(ACServerConfiguration configuration, TcpClient client, ChatService chatService, Func<RconClient, int, RconCommandContext> rconContextFactory)
    {
        _client = client;
        _chatService = chatService;
        _rconContextFactory = rconContextFactory;
        _configuration = configuration;
        _stream = client.GetStream();
    }
    
    internal Task StartAsync()
    {
        SendLoopTask = Task.Run(SendLoopAsync);
        _ = Task.Run(ReceiveLoopAsync);

        return Task.CompletedTask;
    }
    
    public void SendPacket<TPacket>(TPacket packet) where TPacket : IOutgoingNetworkPacket
    {
        try
        {
            if (!_outgoingPacketChannel.Writer.TryWrite(packet) && !_isDisconnectRequested)
            {
                Log.Warning("Cannot write packet to RCON packet queue, disconnecting");
                _ = DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending RCON packet");
        }
    }
    
    private async Task SendLoopAsync()
    {
        try
        {
            await foreach (var packet in _outgoingPacketChannel.Reader.ReadAllAsync(_disconnectTokenSource.Token))
            {
                PacketWriter writer = new PacketWriter(_stream, _tcpSendBuffer, true);
                writer.WritePacket(packet);

                await writer.SendAsync(_disconnectTokenSource.Token);
            }
        }
        catch (ChannelClosedException) { }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending RCON packet");
            _ = DisconnectAsync();
        }
    }

    private async Task ReceiveLoopAsync()
    {
        byte[] buffer = new byte[2046];

        try
        {
            while (!_disconnectTokenSource.IsCancellationRequested)
            {
                var reader = new PacketReader(_stream, buffer, true);
                reader.SliceBuffer(await reader.ReadPacketAsync());

                if (reader.Buffer.Length == 0)
                    return;

                int requestId = reader.Read<int>();
                RconProtocolIn type = (RconProtocolIn)reader.Read<int>();

                if (!_isAuthenticated && type != RconProtocolIn.Auth)
                    return;

                if (!_isAuthenticated)
                {
                    AuthPacket authPacket = reader.ReadPacket<AuthPacket>();

                    if (_configuration.Server.CheckAdminPassword(authPacket.RconPassword))
                    {
                        Log.Debug("Accepted RCON connection from {IpEndpoint}", _client.Client.RemoteEndPoint?.ToString());
                        _isAuthenticated = true;
                        SendPacket(new AuthResponsePacket { RequestId = requestId });
                    }
                    else
                    {
                        SendPacket(new AuthResponsePacket { RequestId = -1 });
                    }

                    if (!_isAuthenticated)
                        return;
                }
                else if (_isAuthenticated)
                {
                    if (type == RconProtocolIn.ExecCommand)
                    {
                        var packet = reader.ReadPacket<ExecCommandPacket>();
                        Log.Debug("RCON ({IpEndpoint}): {Command}", _client.Client.RemoteEndPoint?.ToString(),
                            packet.Command);
                        var context = _rconContextFactory(this, requestId);
                        await _chatService.ProcessCommandAsync(context, packet.Command);
                        context.SendRconResponse();
                    }
                }
            }
        }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
        catch (Exception ex)
        {
            Log.Error(ex, "Error receiving RCON packet");
        }
        finally
        {
            await DisconnectAsync();
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            if (_isDisconnectRequested)
                return;

            _isDisconnectRequested = true;

            _outgoingPacketChannel.Writer.TryComplete();
            _ = await Task.WhenAny(Task.Delay(2000), SendLoopTask);

            try
            {
                _disconnectTokenSource.Cancel();
                _disconnectTokenSource.Dispose();
            }
            catch (ObjectDisposedException) { }
            
            _client.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error disconnecting RCON client");
        }
    }
}
