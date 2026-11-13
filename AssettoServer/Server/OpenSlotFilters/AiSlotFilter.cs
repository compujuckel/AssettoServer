using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Configuration.Kunos;

namespace AssettoServer.Server.OpenSlotFilters;

public class AiSlotFilter : OpenSlotFilterBase
{
    private readonly EntryCarManager _entryCarManager;
    private readonly ACServerConfiguration _configuration;

    public AiSlotFilter(EntryCarManager entryCarManager, ACServerConfiguration configuration)
    {
        _entryCarManager = entryCarManager;
        _configuration = configuration;
    }

    public override async ValueTask<bool> IsSlotOpenAsync(EntryList.Entry entry, ulong guid)
    {
        if (entry.AiMode == AiMode.Fixed
            || (_configuration.Extra.AiParams.MaxPlayerCount > 0 && _entryCarManager.ConnectedCars.Count >= _configuration.Extra.AiParams.MaxPlayerCount))
        {
            return false;
        }
        
        return await base.IsSlotOpenAsync(entry, guid);
    }
}
