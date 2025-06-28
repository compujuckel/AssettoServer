using System.Numerics;
using System.Reflection;
using System.Text.Json;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Ai.Splines;
using AssettoServer.Server.Configuration;
using AssettoServer.Utils;
using FastTravelPlugin.Packets;
using Microsoft.Extensions.Hosting;

namespace FastTravelPlugin;

public class FastTravelPlugin : IHostedService
{
    private readonly AiSpline _aiSpline;

    public FastTravelPlugin(FastTravelConfiguration configuration,
        ACServerConfiguration serverConfiguration,
        CSPServerScriptProvider scriptProvider,
        CSPClientMessageTypeManager cspClientMessageTypeManager,
        AiSpline? aiSpline = null)
    {
        _aiSpline = aiSpline ?? throw new ConfigurationException("FastTravelPlugin does not work with AI traffic disabled");
        
        if (configuration.RequireCollisionDisable && serverConfiguration.CSPTrackOptions.MinimumCSPVersion < CSPVersion.V0_2_8)
        {
            throw new ConfigurationException("FastTravelPlugin needs a minimum required CSP version of 0.2.8 (3424)");
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
        scriptProvider.AddScript(reconnectScript, "fasttravel.lua", new Dictionary<string, object>
        {
            ["mapFixedTargetPosition"] = $"\"{JsonSerializer.Serialize(configuration.MapFixedTargetPosition)}\"",
            ["mapZoomValues"] = $"\"{JsonSerializer.Serialize(configuration.MapZoomValues)}\"",
            ["mapMoveSpeeds"] = $"\"{JsonSerializer.Serialize(configuration.MapMoveSpeeds)}\"",
            ["showMapImg"] = configuration.ShowMapImage ? "true" : "false"
        });
        
        cspClientMessageTypeManager.RegisterOnlineEvent<FastTravelPacket>(OnFastTravelPacket);
    }

    private void OnFastTravelPacket(ACTcpClient client, FastTravelPacket packet)
    {
        var (splinePointId, _) = _aiSpline.WorldToSpline(packet.Position);

        var splinePoint = _aiSpline.Points[splinePointId];
        
        var direction = - _aiSpline.Operations.GetForwardVector(splinePoint.Id);
        if (direction == Vector3.Zero)
            direction = new Vector3(1, 0, 0);
        
        client.SendPacket(new FastTravelPacket
        {
            Position = packet.Position,
            Direction = direction,
            SessionId = 255
        });
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
