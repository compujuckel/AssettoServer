using System.Threading.Tasks;
using AssettoServer.Shared.Model;

namespace AssettoServer.Server.OpenSlotFilters;

public class GuidSlotFilter : OpenSlotFilterBase
{
    public override async ValueTask<bool> IsSlotOpen(IEntryCar<IClient> entryCar, ulong guid)
    {
        if (entryCar.AllowedGuids.Count > 0 && !entryCar.AllowedGuids.Contains(guid))
        {
            return false;
        }

        return await base.IsSlotOpen(entryCar, guid);
    }
}
