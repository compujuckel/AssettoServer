using System;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AssettoServer.Server.Ai;

public class AiRandomNameService : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly ACServerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    
    public AiRandomNameService(IHostApplicationLifetime applicationLifetime,
        ACServerConfiguration configuration,
        EntryCarManager entryCarManager) : base(applicationLifetime)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
    }
    
    private async Task AiRandomNameLoopAsync(EntryCar car, CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!car.AiControlled) continue;
                
                car.AiName = _configuration.Extra.AiParams.RandomTrafficNames![
                    Random.Shared.Next(0, _configuration.Extra.AiParams.RandomTrafficNames.Count)];
                _entryCarManager.BroadcastPacket(new CarConnected
                {
                    SessionId = car.SessionId,
                    Name = car.AiName
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error changing random AI name");
            }
            finally
            {
                await Task.Delay(Random.Shared.Next(300_000, 1_800_000), token);
            }
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_configuration.Extra.AiParams.RandomTrafficNames == null ||
            _configuration.Extra.AiParams.RandomTrafficNames.Count == 0)
            return Task.CompletedTask;
        
        foreach (var car in _entryCarManager.EntryCars)
        {
            _ = Task.Run(() => AiRandomNameLoopAsync(car, stoppingToken), stoppingToken);
        }

        return Task.CompletedTask;
    }
}
