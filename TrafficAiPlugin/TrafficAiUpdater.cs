using AssettoServer.Network.ClientMessages;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Services;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;
using TrafficAiConfiguration = TrafficAIPlugin.Configuration.TrafficAiConfiguration;

namespace TrafficAIPlugin;

public class TrafficAiUpdater
{
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _sessionManager;
    private readonly TrafficAi _trafficAi;

    public TrafficAiUpdater(
        EntryCarManager entryCarManager,
        SessionManager sessionManager,
        ACServer server,
        TrafficAi trafficAi)
    {
        _entryCarManager = entryCarManager;
        _sessionManager = sessionManager;
        _trafficAi = trafficAi;

        server.Update += OnUpdate;
    }

    private void OnUpdate(object sender, EventArgs args)
    {
        try
        {
            foreach (var instance in _trafficAi.Instances)
            {
                instance.AiUpdate();
            }
            
            Dictionary<EntryCar, List<PositionUpdateOut>> positionUpdates = new();
            foreach (var entryCar in _entryCarManager.EntryCars)
            {
                positionUpdates[entryCar] = [];
            }
            
            foreach (var fromCar in _trafficAi.Instances)
            {
                if (!fromCar.EntryCar.AiControlled) continue;
                
                foreach (var (_, toCar) in _entryCarManager.ConnectedCars)
                {
                    var toClient = toCar.Client;
                    if (toCar == fromCar.EntryCar 
                        || toClient == null || !toClient.HasSentFirstUpdate || !toClient.HasUdpEndpoint
                        || !fromCar.GetPositionUpdateForCar(toCar, out var update)) continue;

                    if (toClient.SupportsCSPCustomUpdate)
                    {
                        positionUpdates[toCar].Add(update);
                    }
                    else
                    {
                        toClient.SendPacketUdp(in update);
                    }
                }
            }

            foreach (var (toCar, updates) in positionUpdates)
            {
                if (updates.Count == 0) continue;
                    
                var toClient = toCar.Client;
                if (toClient == null) continue;
                
                const int chunkSize = 20;
                for (int i = 0; i < updates.Count; i += chunkSize)
                {
                    if (toClient.SupportsCSPCustomUpdate)
                    {
                        var packet = new CSPPositionUpdate(new ArraySegment<PositionUpdateOut>(updates.ToArray(), i, Math.Min(chunkSize, updates.Count - i)));
                        toClient.SendPacketUdp(in packet);
                    }
                    else
                    {
                        var packet = new BatchedPositionUpdate((uint)(_sessionManager.ServerTimeMilliseconds - toCar.TimeOffset), toCar.Ping,
                            new ArraySegment<PositionUpdateOut>(updates.ToArray(), i, Math.Min(chunkSize, updates.Count - i)));
                        toClient.SendPacketUdp(in packet);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during ghost car update");
        }
    }
}
