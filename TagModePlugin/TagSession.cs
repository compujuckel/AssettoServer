using System.Drawing;
using AssettoServer.Server;
using Microsoft.Net.Http.Headers;
using Serilog;

namespace TagModePlugin;

public class TagSession
{
    public EntryCar InitialTagger { get; }
    public EntryCar LastCaught { get; set; }

    private bool HasStarted { get; set; }
    private bool IsCancelled { get; set; }
    public bool HasEnded { get; private set; }
    private long StartTimeMilliseconds { get; set; }
    
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly TagModePlugin _plugin;
    private readonly TagModeConfiguration _configuration;

    public delegate TagSession Factory(EntryCar initialTagger);
    
    public TagSession(EntryCar initialTagger,
        SessionManager sessionManager,
        EntryCarManager entryCarManager,
        TagModePlugin plugin,
        TagModeConfiguration configuration)
    {
        InitialTagger = LastCaught = initialTagger;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
        _plugin = plugin;
        _configuration = configuration;
    }

    public Task StartAsync()
    {
        if (!HasStarted)
        {
            HasStarted = true;
            _ = Task.Run(SessionAsync);
        }

        return Task.CompletedTask;
    }

    private async Task SessionAsync()
    {
        try
        {
            UpdateAllColors(_plugin.RunnerColor);
            
            _entryCarManager.BroadcastChat("Game of tag starting in 15 seconds");
            await Task.Delay(15_000);
            
            _entryCarManager.BroadcastChat("Ready...");
            await Task.Delay(1_000);
            _entryCarManager.BroadcastChat("Set...");
            await Task.Delay(1_000);
            _entryCarManager.BroadcastChat("Run!");
            
            _plugin.Instances[InitialTagger.SessionId].SetTagged();
            StartTimeMilliseconds = _sessionManager.ServerTimeMilliseconds;
            
            while (!IsCancelled)
            {
                switch (_configuration.EnableEndlessMode)
                {
                    case false:
                        if (_sessionManager.ServerTimeMilliseconds - StartTimeMilliseconds > _configuration.SessionDurationMilliseconds ||
                              _plugin.Instances.Where(car => car.Value.IsConnected ).All(car => car.Value.IsTagged))
                            return;
                        break;
                    case true:
                        if (_plugin.Instances.Where(car => car.Value.IsConnected).All(car => car.Value.IsTagged))
                            return;
                        break;
                }

                await Task.Delay(250);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while running the session");
            IsCancelled = true;
        }
        finally
        {
            await FinishSession();
        }
    }

    private async Task FinishSession()
    {
        if (IsCancelled)
        {
            _entryCarManager.BroadcastChat("The game of tag was cancelled.");
            Log.Information("The game of tag was cancelled");
        }
        else
        {
            switch (_configuration.EnableEndlessMode)
            {
                case false:
                    var winners = _plugin.Instances.Any(car => car.Value is { IsTagged: false, IsConnected: true }) ? "Runners" : "Taggers";

                    _entryCarManager.BroadcastChat($"The {winners} just won this game of tag.");
                    Log.Information("The {Winners} just won this game of tag", winners);
                    break;
                case true:
                    var winner = LastCaught.Client?.Name ?? $"Car #{LastCaught.SessionId}";

                    _entryCarManager.BroadcastChat($"'{winner}' just won this game of tag.");
                    Log.Information("{Winner} just won this game of tag", winner);
                    break;
            }
        }
        
        await Task.Delay(15_000);
        
        UpdateAllColors(_plugin.NeutralColor);

        foreach (var car in _plugin.Instances.Values)
        {
            car.SetTagged(false);
        }
        
        HasEnded = true;
    }

    private void UpdateAllColors(Color color)
    {
        foreach (var car in _plugin.Instances.Values.Where(car => car.IsConnected))
        {
            car.UpdateColor(color);
        }
    }

    public void Cancel() => IsCancelled = true;
    
    internal EntryCarTagMode GetCar(EntryCar entryCar) => _plugin.Instances[entryCar.SessionId];
}
