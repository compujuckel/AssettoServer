using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using Microsoft.Extensions.Hosting;

namespace TagModePlugin;

public class TagModePlugin : BackgroundService
{
    private const int MinPlayers = 2;
    
    private readonly TagModeConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly TagSession.Factory _sessionFactory;
    private readonly Func<EntryCar, EntryCarTagMode> _entryCarTagModeFactory;
    
    public TagSession? CurrentSession { get; private set; }

    public readonly Dictionary<int, EntryCarTagMode> Instances = new();
    public readonly Color TaggedColor;
    public readonly Color RunnerColor;
    public readonly Color NeutralColor;
    
    public TagModePlugin(ACServerConfiguration acServerConfiguration,
        TagModeConfiguration configuration,
        EntryCarManager entryCarManager,
        Func<EntryCar, EntryCarTagMode> entryCarTagModeFactory,
        TagSession.Factory sessionFactory,
        CSPServerScriptProvider scriptProvider)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _entryCarTagModeFactory = entryCarTagModeFactory;
        _sessionFactory = sessionFactory;
        
        _entryCarManager.ClientConnected += (sender, _) =>
        {
            sender.Collision += OnCollision;
            sender.Disconnecting += OnDisconnecting;
            sender.LuaReady += OnLuaReady;
        };

        if (!acServerConfiguration.Extra.EnableClientMessages)
        {
            throw new ConfigurationException("TagModePlugin requires enabled client messages");
        }

        scriptProvider.AddScript(Assembly.GetExecutingAssembly().GetManifestResourceStream("TagModePlugin.lua.tagmode.lua")!, "tagmode.lua");
        
        TaggedColor = ColorTranslator.FromHtml(_configuration.TaggedColor);
        RunnerColor = ColorTranslator.FromHtml(_configuration.RunnerColor);
        NeutralColor = ColorTranslator.FromHtml(_configuration.NeutralColor);
    }

    private void OnDisconnecting(ACTcpClient sender, EventArgs args)
        => Instances[sender.SessionId].OnDisconnecting();

    private void OnLuaReady(ACTcpClient sender, EventArgs args)
        => Instances[sender.SessionId].OnLuaReady();

    private void OnCollision(ACTcpClient sender, CollisionEventArgs args)
        => Instances[sender.SessionId].OnCollision(args);

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_entryCarManager.EntryCars.Any(entryCar => entryCar.DriverOptionsFlags.HasFlag(DriverOptionsFlags.AllowColorChange)))
        {
            throw new ConfigurationException("TagModePlugin doesn't support driver selected car color changes");
        }
        
        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            Instances.Add(entryCar.SessionId, _entryCarTagModeFactory(entryCar));
        }
        
        await base.StartAsync(cancellationToken);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.EnableLoop) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (await TryStartSession())
            {
                await WaitForSessionToEnd(stoppingToken);
                await Task.Delay(_configuration.SessionPauseIntervalMilliseconds, stoppingToken);
            }
            
            await Task.Delay(10000, stoppingToken);
        }
    }
    
    public bool TryPickRandomTagger([NotNullWhen(true)] out EntryCar? randomTagger)
    {
        var players = _entryCarManager.EntryCars.Where(car => car.Client is { HasSentFirstUpdate: true }).ToList();
        if (players.Count < MinPlayers)
        {
            randomTagger = null;
            return false;
        }
        
        randomTagger = players[Random.Shared.Next(players.Count)];
        return true;
    }

    public async Task<bool> TryStartSession(EntryCar? tagger = null)
    {
        if (CurrentSession is { HasEnded: false }) return false;
        
        if (_entryCarManager.EntryCars.Count(car => car.Client is { HasSentFirstUpdate: true }) >= MinPlayers)
        {
            if (tagger is null && !TryPickRandomTagger(out tagger))
                return false;
            
            CurrentSession = _sessionFactory(tagger);
            await CurrentSession.StartAsync();
            return true;
        }

        return false;
    }

    private async Task WaitForSessionToEnd(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            if (CurrentSession is { HasEnded: true })
                return;
        }
    }
}
