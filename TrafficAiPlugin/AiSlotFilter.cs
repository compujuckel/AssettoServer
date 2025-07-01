using AssettoServer.Server;
using AssettoServer.Server.OpenSlotFilters;
using TrafficAiPlugin.Configuration;

namespace TrafficAiPlugin;

public class AiSlotFilter : OpenSlotFilterBase
{
    private readonly EntryCarManager _entryCarManager;
    private readonly TrafficAiConfiguration _configuration;

    public AiSlotFilter(EntryCarManager entryCarManager, TrafficAiConfiguration configuration)
    {
        _entryCarManager = entryCarManager;
        _configuration = configuration;
    }

    public override async ValueTask<bool> IsSlotOpen(EntryCar entryCar, ulong guid)
    {
        if (entryCar.AiMode == AiMode.Fixed
            || (_configuration.MaxPlayerCount > 0 && _entryCarManager.ConnectedCars.Count >= _configuration.MaxPlayerCount))
        {
            return false;
        }
        
        return await base.IsSlotOpen(entryCar, guid);
    }
}
