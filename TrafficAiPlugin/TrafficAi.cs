using AssettoServer.Network.ClientMessages;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Services;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;
using TrafficAiConfiguration = TrafficAIPlugin.Configuration.TrafficAiConfiguration;

namespace TrafficAIPlugin;

public class TrafficAi : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly TrafficAiConfiguration _configuration;
    private readonly ACServerConfiguration _serverConfiguration;

    public TrafficAi(TrafficAiConfiguration configuration,
        ACServerConfiguration serverConfiguration,
        ACServer server,
        CSPClientMessageTypeManager cspClientMessageTypeManager,
        IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _configuration = configuration;
        _serverConfiguration = serverConfiguration;

        if (_configuration.EnableCarReset)
        {
            if (!_serverConfiguration.Extra.EnableClientMessages || _serverConfiguration.CSPTrackOptions.MinimumCSPVersion < CSPVersion.V0_2_3_p47)
            {
                throw new ConfigurationException(
                    "Reset car: Minimum required CSP version of 0.2.3-preview47 (2796); Requires enabled client messages; Requires working AI spline");
            }
            cspClientMessageTypeManager.RegisterOnlineEvent<RequestResetPacket>((client, _) => { OnResetCar(client); });
        }


        server.Update += MainLoop;
    }

    private void OnResetCar(ACTcpClient sender)
    {
        if (_configuration.EnableCarReset)
            sender.EntryCar.TryResetPosition();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Debug("Sample plugin autostart called");
        return Task.CompletedTask;
    }

    private void MainLoop(ACServer server, EventArgs args)
    {
        
    }
}
