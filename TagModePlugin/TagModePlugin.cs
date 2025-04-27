using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;
using TagModePlugin.Packets;

namespace TagModePlugin;

public class TagModePlugin : CriticalBackgroundService, IAssettoServerAutostart
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
        CSPServerScriptProvider scriptProvider,
        IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _entryCarTagModeFactory = entryCarTagModeFactory;
        _sessionFactory = sessionFactory;
        
        _entryCarManager.ClientConnected += (sender, _) =>
        {
            sender.FirstUpdateSent += OnFirstUpdateSent;
            sender.Collision += OnCollision;
            sender.Disconnecting += OnDisconnecting;
        };

        if (!acServerConfiguration.Extra.EnableClientMessages)
        {
            throw new ConfigurationException("TagModePlugin requires enabled client messages");
        }

        using var streamReader = new StreamReader(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("TagModePlugin.lua.tagmode.lua")!);
        var reconnectScript = streamReader.ReadToEnd();
        scriptProvider.AddScript(reconnectScript, "tagmode.lua");
        
        TaggedColor = ColorTranslator.FromHtml(_configuration.TaggedColor);
        RunnerColor = ColorTranslator.FromHtml(_configuration.RunnerColor);
        NeutralColor = ColorTranslator.FromHtml(_configuration.NeutralColor);
    }

    private void OnDisconnecting(ACTcpClient sender, EventArgs args)
        => Instances[sender.SessionId].OnDisconnecting();

    private void OnFirstUpdateSent(ACTcpClient sender, EventArgs args)
        => _ = Instances[sender.SessionId].OnFirstUpdateSent();

    private void OnCollision(ACTcpClient sender, CollisionEventArgs args)
        => Instances[sender.SessionId].OnCollision(args);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_entryCarManager.EntryCars.Any(entryCar => entryCar.DriverOptionsFlags.HasFlag(DriverOptionsFlags.AllowColorChange)))
        {
            throw new ConfigurationException("TagModePlugin doesn't support driver selected car color changes");
        }
        
        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            Instances.Add(entryCar.SessionId, _entryCarTagModeFactory(entryCar));
        }

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
        randomTagger = null;
        
        float weightSum = _entryCarManager.EntryCars.Count(car => car.Client is { HasSentFirstUpdate: true });
        if (weightSum < MinPlayers) return false;

        float prefixSum = 0.0f;
        List<(EntryCar Car, float PrefixSum)> players = [];
        foreach (var car in _entryCarManager.EntryCars.Where(car => car.Client is { HasSentFirstUpdate: true }))
        {
            prefixSum += 1 / weightSum;
            players.Add((car, prefixSum));
        }
        
        float rng = Random.Shared.NextSingle();

        int begin = 0, end = players.Count;
        while (begin <= end)
        {
            int i = (begin + end) / 2;

            if (players[i].PrefixSum <= rng)
            {
                begin = i + 1;
            }
            else
            {
                end = i - 1;
                randomTagger = players[i].Car;
            }
        }

        return randomTagger != null;
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
