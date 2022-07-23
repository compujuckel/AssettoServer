namespace AssettoServer.Server.OpenSlotFilters;

public class GuidSlotFilter : OpenSlotFilterBase
{
    public override bool IsSlotOpen(EntryCar entryCar, ulong guid)
    {
        if (entryCar.AllowedGuids.Count > 0 && !entryCar.AllowedGuids.Contains(guid))
        {
            return false;
        }

        return base.IsSlotOpen(entryCar, guid);
    }
}
