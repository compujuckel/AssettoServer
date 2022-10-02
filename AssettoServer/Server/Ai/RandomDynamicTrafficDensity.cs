using System;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using AssettoServer.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AssettoServer.Server.Ai;

public class RandomDynamicTrafficDensity : CriticalBackgroundService
{
    private readonly ACServerConfiguration _configuration;
    private readonly WeatherManager _weatherManager;
    //private DateTime _nextDensityChangeTime = new DateTime();
    private float _currentDensity = 1;
    private readonly Random _random = new Random();
    private readonly EntryCarManager _entryCarManager;

    public RandomDynamicTrafficDensity(ACServerConfiguration configuration, WeatherManager weatherManager, IHostApplicationLifetime applicationLifetime, EntryCarManager entryCarManager) : base(applicationLifetime)
    {
        _configuration = configuration;
        _weatherManager = weatherManager;
        _entryCarManager = entryCarManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                //if (DateTime.Now >= _nextDensityChangeTime)
                //{
                    //var randMinutes = _random.Next(_configuration.Extra.AiParams.MinRandomTrafficDensityMinutes ?? 1, (_configuration.Extra.AiParams.MaxRandomTrafficDensityMinutes ?? 1) + 1);
                    var randDensity = GetRandomNumber(_configuration.Extra.AiParams.MinRandomTrafficDensity ?? 1, _configuration.Extra.AiParams.MaxRandomTrafficDensity ?? 2);
                    //_nextDensityChangeTime = DateTime.Now.AddMinutes(randMinutes);
                    _currentDensity = randDensity;
                //}                
                Log.Debug("New density: " + _currentDensity.ToString());
                _configuration.Extra.AiParams.TrafficDensity = _currentDensity;
                _configuration.TriggerReload();

                var ratio = (_currentDensity - _configuration.Extra.AiParams.MinRandomTrafficDensity ?? 1) / (_configuration.Extra.AiParams.MaxRandomTrafficDensity ?? 2 - _configuration.Extra.AiParams.MinRandomTrafficDensity ?? 1);
                if (ratio <= 0.25) Broadcast("Traffic is clearing up.");
                else if (ratio <= 0.5) Broadcast("Casual traffic up ahead.");
                else if (ratio <= 0.75) Broadcast("Peak traffic, slow down.");
                else if (ratio <= 1) Broadcast("Possibly an accident up ahead, traffic is packed");

            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in random dynamic traffic density update");
            }
            finally
            {
                var randMinutes = _random.Next(_configuration.Extra.AiParams.MinRandomTrafficDensityMinutes ?? 1, (_configuration.Extra.AiParams.MaxRandomTrafficDensityMinutes ?? 1) + 1);
                await Task.Delay(TimeSpan.FromMinutes(randMinutes), stoppingToken);
            }
        }
    }

    public float GetRandomNumber(float minimum, float maximum)
    {
        Random random = new Random();
        return (float)random.NextDouble() * (maximum - minimum) + minimum;
    }

    public void Broadcast(string message)
    {
        Log.Information("Broadcast: {Message}", message);        
        _entryCarManager.BroadcastPacket(new ChatMessage { SessionId = 255, Message = message });
    }
}
