using System.Reflection;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Ai.Splines;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AutoModerationPlugin;

public class AutoModerationPlugin : BackgroundService
{
    private readonly List<EntryCarAutoModeration> _instances = [];
    private readonly AutoModerationConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly WeatherManager _weatherManager;
    private readonly Func<EntryCar, EntryCarAutoModeration> _entryCarAutoModerationFactory;

    public AutoModerationPlugin(AutoModerationConfiguration configuration,
        EntryCarManager entryCarManager,
        WeatherManager weatherManager,
        ACServerConfiguration serverConfiguration,
        CSPServerScriptProvider scriptProvider,
        Func<EntryCar, EntryCarAutoModeration> entryCarAutoModerationFactory,
        AiSpline? aiSpline = null)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _weatherManager = weatherManager;
        _entryCarAutoModerationFactory = entryCarAutoModerationFactory;

        if (aiSpline == null)
        {
            if (_configuration.WrongWayPenalty.Enabled)
            {
                throw new ConfigurationException("AutoModerationPlugin: Wrong way kick does not work with AI traffic disabled");
            }

            if (_configuration.BlockingRoadPenalty.Enabled)
            {
                throw new ConfigurationException("AutoModerationPlugin: Blocking road kick does not work with AI traffic disabled");
            }
        }
        
        if (serverConfiguration.Extra.EnableClientMessages)
        {
            scriptProvider.AddScript(Assembly.GetExecutingAssembly().GetManifestResourceStream("AutoModerationPlugin.lua.automoderation.lua")!, "automoderation.lua");
        }

        if (_configuration.AfkPenalty is { Enabled: true, Behavior: AfkPenaltyBehavior.MinimumSpeed })
        {
            _entryCarManager.ClientConnected += (sender, _) => sender.FirstUpdateSent += OnFirstUpdateSent;
        }
        _entryCarManager.ClientConnected += (sender, _) => sender.LoggedInAsAdministrator += OnAdminLoggedIn;
    }

    private void OnFirstUpdateSent(ACTcpClient sender, EventArgs args)
    {
        _instances[sender.SessionId].SetActive();
    }

    private void OnAdminLoggedIn(ACTcpClient sender, EventArgs args)
    {
        _instances[sender.SessionId].AdminReset();
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            _instances.Add(_entryCarAutoModerationFactory(entryCar));
        }
        
        if (_configuration.NoLightsPenalty.Enabled && !_weatherManager.CurrentSunPosition.HasValue)
        {
            throw new ConfigurationException("AutoModerationPlugin: No lights kick does not work with missing track params");
        }
        
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                foreach (var instance in _instances)
                {
                    instance.Update();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during auto moderation update");
            }
        }
    }
}
