using System.Threading.Tasks;
using AssettoServer.Server.Configuration.Kunos;

namespace AssettoServer.Server.OpenSlotFilters;

public class GuidSlotFilter : OpenSlotFilterBase
{
    public override async ValueTask<bool> IsSlotOpenAsync(EntryList.Entry entry, ulong guid)
    {
        if (entry.AllowedGuids.Count > 0 && !entry.AllowedGuids.Contains(guid))
        {
            return false;
        }

        return await base.IsSlotOpenAsync(entry, guid);
    }
}
