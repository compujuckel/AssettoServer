using AssettoServer.Server;
using Microsoft.Extensions.Hosting;

namespace RaceChallengePlugin;

public class RaceChallengePlugin : IHostedService
{
    private readonly EntryCarManager _entryCarManager;
    private readonly Func<EntryCar, EntryCarRace> _entryCarRaceFactory;
    private readonly Dictionary<int, EntryCarRace> _instances = new();
    
    internal EntryCarRace GetRace(EntryCar entryCar) => _instances[entryCar.SessionId];
    
    public RaceChallengePlugin(EntryCarManager entryCarManager, Func<EntryCar, EntryCarRace> entryCarRaceFactory)
    {
        _entryCarManager = entryCarManager;
        _entryCarRaceFactory = entryCarRaceFactory;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            _instances.Add(entryCar.SessionId, _entryCarRaceFactory(entryCar));
        }
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
