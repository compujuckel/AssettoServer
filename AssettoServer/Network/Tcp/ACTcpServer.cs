using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Services;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AssettoServer.Network.Tcp;

public class ACTcpServer : CriticalBackgroundService
{
    private readonly ACServerConfiguration _configuration;
    private readonly Func<TcpClient, ACTcpClient> _acTcpClientFactory;

    public ACTcpServer(Func<TcpClient, ACTcpClient> acTcpClientFactory, ACServerConfiguration configuration, IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _acTcpClientFactory = acTcpClientFactory;
        _configuration = configuration;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Starting TCP server on port {TcpPort}", _configuration.Server.TcpPort);
        var listener = new TcpListener(IPAddress.Any, _configuration.Server.TcpPort);
        listener.Start();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                TcpClient tcpClient = await listener.AcceptTcpClientAsync(stoppingToken);

                ACTcpClient acClient = _acTcpClientFactory(tcpClient);
                await acClient.StartAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Error(ex, "Something went wrong while trying to accept TCP connection");
            }
        }
    }
}
