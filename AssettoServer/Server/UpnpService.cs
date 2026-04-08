using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using Microsoft.Extensions.Hosting;
using Open.Nat;
using Serilog;

namespace AssettoServer.Server;

public class UpnpService : IHostedService
{
    private readonly List<Mapping> _mappings;
    private readonly bool _isEnabled;
    private readonly bool _logErrors;
    private NatDevice? _natDevice;
    
    public UpnpService(ACServerConfiguration configuration)
    {
        _logErrors = configuration.Extra.EnableUPnP == true;
        _isEnabled = _logErrors || (!configuration.Extra.EnableUPnP.HasValue &&
                                    configuration.Server.RegisterToLobby &&
                                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        _mappings =
        [
            new Mapping(Protocol.Tcp, configuration.Server.TcpPort, configuration.Server.TcpPort, "AssettoServer"),
            new Mapping(Protocol.Udp, configuration.Server.UdpPort, configuration.Server.UdpPort, "AssettoServer"),
            new Mapping(Protocol.Tcp, configuration.Server.HttpPort, configuration.Server.HttpPort, "AssettoServer")
        ];
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_isEnabled) return;
        
        try
        {
            var discoverer = new NatDiscoverer();
            _natDevice = await discoverer.DiscoverDeviceAsync();
            foreach (var mapping in _mappings)
            {
                await _natDevice.CreatePortMapAsync(mapping);
            }
            Log.Information("Successfully created UPnP port mappings");
        }
        catch (Exception ex)
        {
            const string message = "Failed to create UPnP port mappings";
            if (_logErrors)
            {
                Log.Error(ex, message);
            }
            else
            {
                Log.Warning(message);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_natDevice == null) return;

        try
        {
            foreach (var mapping in _mappings)
            {
                await _natDevice.DeletePortMapAsync(mapping);
            }
        }
        catch (Exception ex)
        {
            const string message = "Failed to delete UPnP port mappings";
            if (_logErrors)
            {
                Log.Error(ex, message);
            }
            else
            {
                Log.Warning(message);
            }
        }
    }
}
