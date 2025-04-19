using System.Drawing;
using AssettoServer.Server;
using Serilog;

namespace TagModePlugin;

public class TagSession
{
    public EntryCar InitialTagger { get; }

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
        InitialTagger = initialTagger;
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
            
            byte signalStage = 0;
            while(signalStage < 3)
            {
                if (signalStage == 0)
                    _entryCarManager.BroadcastChat("Ready...");
                else if (signalStage == 1)
                    _entryCarManager.BroadcastChat("Set...");
                else if (signalStage == 2)
                {
                    _entryCarManager.BroadcastChat("Run!");
                    break;
                }

                await Task.Delay(1_000);
                signalStage++;
            }

            _plugin.Instances[InitialTagger.SessionId].SetTagged();
            StartTimeMilliseconds = _sessionManager.ServerTimeMilliseconds;
            
            while (true)
            {
                if (IsCancelled ||
                    _sessionManager.ServerTimeMilliseconds - StartTimeMilliseconds > _configuration.SessionDurationMilliseconds ||
                    _plugin.Instances.Where(car => car.Value.IsConnected ).All(car => car.Value.IsTagged))
                    return;

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
            var winners = _plugin.Instances.Any(car => car.Value is { IsTagged: false, IsConnected: true }) ? "Runners" : "Taggers";

            _entryCarManager.BroadcastChat($"The {winners} just won this game of tag.");
            Log.Information("The {Winners} just won this game of tag", winners);
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
