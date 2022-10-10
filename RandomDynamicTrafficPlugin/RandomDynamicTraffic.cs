using AssettoServer.Network.Packets.Shared;
using AssettoServer.Server;
using AssettoServer.Server.Ai;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Utils;
using Humanizer.Configuration;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RandomDynamicTrafficPlugin.Packets;
using Serilog;
using System;
using System.Reflection;
using System.Runtime.ConstrainedExecution;

namespace RandomDynamicTrafficPlugin
{
    [UsedImplicitly]
    public class RandomDynamicTraffic : CriticalBackgroundService, IAssettoServerAutostart
    {
        private readonly ACServerConfiguration _acServerConfiguration;
        private readonly RandomDynamicTrafficConfiguration _configuration;
        private readonly EntryCarManager _entryCarManager;
        private float _currentDensity = 1;
        private readonly Random _random = new Random();        
        private bool _isEnabled = false;
        private List<EntryCar> _cars = new List<EntryCar>();
        private readonly RandomDynamicTrafficHelperFunctions _randomDynamicTrafficHelperFunctions;

        public RandomDynamicTraffic(RandomDynamicTrafficConfiguration configuration,
            IHostApplicationLifetime applicationLifetime,
            ACServerConfiguration acServerConfiguration,
            EntryCarManager entryCarManager,
            CSPServerScriptProvider scriptProvider,
            TrafficMap trafficMap) : base(applicationLifetime)
        {            
            _configuration = configuration;
            _acServerConfiguration = acServerConfiguration;
            _entryCarManager = entryCarManager;
            _randomDynamicTrafficHelperFunctions = new RandomDynamicTrafficHelperFunctions(_configuration, acServerConfiguration, trafficMap, entryCarManager);

            _cars = new List<EntryCar>();
            foreach (var car in _entryCarManager.EntryCars)
            {
                _cars.Add(car);                
            }

            _isEnabled = configuration.IsEnabled;
            Log.Debug("RandomDynamicTraffic plugin constructor called! Hello: {Hello}", configuration.Hello);
            Log.Debug("Loaded RandomDynamicTraffic plugin. Is plugin variables set? {_isEnabled}", _isEnabled);

            if (_acServerConfiguration.Extra.EnableClientMessages)
            {
                using var streamReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("RandomDynamicTrafficPlugin.lua.randomdynamictraffic.lua")!);
                scriptProvider.AddScript(streamReader.ReadToEnd(), "randomdynamictraffic.lua");
            }

            if (_isEnabled)
            {
                var newDensity = _configuration.MaxTrafficDensity / 2;
                UpdateDensity(newDensity);                
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {           
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!_isEnabled) return;
                    var newDensity = _randomDynamicTrafficHelperFunctions.CalculateNewDensity(_cars, _currentDensity);
                    UpdateDensity(newDensity);                    
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in random dynamic traffic density update");
                }
                finally
                {
                    var randSeconds = _random.Next(_configuration.MinAdjustmentsSeconds, (_configuration.MaxAdjustmentsSeconds) + 1);
                    await Task.Delay(TimeSpan.FromSeconds(randSeconds), stoppingToken);
                }
            }
        }

        public void UpdateDensity(float density)
        {
            _currentDensity = density;

            Log.Debug("New density: " + _currentDensity.ToString());
            _acServerConfiguration.Extra.AiParams.TrafficDensity = _currentDensity;
            _acServerConfiguration.TriggerReload();            

            var trafficState = TrafficState.ACCIDENT;
            if (_currentDensity <= _configuration.LowTrafficDensity)
            {
                trafficState = TrafficState.LOW;
            }
            else if (_currentDensity <= _configuration.CasualTrafficDensity)
            {
                trafficState = TrafficState.CASUAL;
            }
            else if (_currentDensity <= _configuration.PeakTrafficDensity)
            {
                trafficState = TrafficState.PEAK;
            }

            foreach (var entryCar in _entryCarManager.EntryCars)
            {
                var client = entryCar.Client;
                if (client == null || !client.HasSentFirstUpdate)
                    continue;
                client.SendPacket(new RandomDynamicTrafficIconPacket
                {
                    TrafficState = trafficState
                });
                Log.Debug("Sent traffic state - {state} - to client - {clientName}", trafficState.ToString(), (client.Name ?? ""));
            }
        }

        public void Broadcast(string message)
        {
            Log.Information("Broadcast: {Message}", message);
            _entryCarManager.BroadcastPacket(new ChatMessage { SessionId = 255, Message = message });
        }
    }

    public class CarsWithStats
    {
        public EntryCar EntryCar;
        public float Speed;

        public CarsWithStats(EntryCar car)
        {
            EntryCar = car;
            Speed = 0;
        }
    }
}
