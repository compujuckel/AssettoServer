using System.Reflection;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Ai.Splines;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Services;
using AssettoServer.Utils;
using FastTravelPlugin.Packets;
using Microsoft.Extensions.Hosting;

namespace FastTravelPlugin;

public class FastTravelPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly AiSpline _aiSpline;

    public FastTravelPlugin(FastTravelConfiguration configuration,
        ACServerConfiguration serverConfiguration,
        CSPServerScriptProvider scriptProvider,
        CSPClientMessageTypeManager cspClientMessageTypeManager,
        IHostApplicationLifetime applicationLifetime,
        AiSpline? aiSpline = null) : base(applicationLifetime)
    {
        _aiSpline = aiSpline ?? throw new ConfigurationException("FastTravelPlugin does not work with AI traffic disabled");
        
        if (configuration.RequireCollisionDisable && serverConfiguration.CSPTrackOptions.MinimumCSPVersion < CSPVersion.V0_2_3_p211)
        {
            throw new ConfigurationException("FastTravelPlugin needs a minimum required CSP version of 0.2.3-preview211 (2974)");
        }
        
        if (!configuration.RequireCollisionDisable && serverConfiguration.CSPTrackOptions.MinimumCSPVersion < CSPVersion.V0_2_0)
        {
            throw new ConfigurationException("FastTravelPlugin needs a minimum required CSP version of 0.2.0 (2651)");
        }

        // Include Client Reconnection Script
        if (!serverConfiguration.Extra.EnableClientMessages)
        {
            throw new ConfigurationException("FastTravelPlugin requires ClientMessages to be enabled");
        }
        
        var luaPath = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lua", "fasttravel.lua");
        
        using var streamReader = new StreamReader(luaPath);
        var reconnectScript = streamReader.ReadToEnd();
        scriptProvider.AddScript(reconnectScript, "fasttravel.lua");
        
        cspClientMessageTypeManager.RegisterOnlineEvent<FastTravelPacket>(OnFastTravelPacket);
    }

    private void OnFastTravelPacket(ACTcpClient client, FastTravelPacket packet)
    {
        var (splinePointId, _) = _aiSpline.WorldToSpline(packet.Position);

        var splinePoint = _aiSpline.Points[splinePointId];
        
        var direction = - _aiSpline.Operations.GetForwardVector(splinePoint.NextId);
        
        client.SendPacket(new FastTravelPacket
        {
            Position = packet.Position,
            Direction = direction,
            SessionId = 255
        });
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
