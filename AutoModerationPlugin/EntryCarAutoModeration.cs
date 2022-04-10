using AssettoServer.Server;
using AssettoServer.Server.Ai;

namespace AutoModerationPlugin;

internal class EntryCarAutoModeration
{
    public EntryCar EntryCar { get; }
    
    public TrafficSplinePoint? CurrentSplinePoint { get; private set; }
    public float CurrentSplinePointDistanceSquared { get; private set; }

    public int NoLightSeconds { get; set; }
    public bool HasSentNoLightWarning { get; set; }

    public int WrongWaySeconds { get; set; }
    public bool HasSentWrongWayWarning { get; set; }
    
    public int BlockingRoadSeconds { get; set; }
    public bool HasSentBlockingRoadWarning { get; set; }

    internal EntryCarAutoModeration(EntryCar entryEntryCar)
    {
        EntryCar = entryEntryCar;
        EntryCar.ResetInvoked += OnResetInvoked;
    }

    private void OnResetInvoked(EntryCar sender, EventArgs args)
    {
        NoLightSeconds = 0;
        HasSentNoLightWarning = false;
        WrongWaySeconds = 0;
        HasSentNoLightWarning = false;
        BlockingRoadSeconds = 0;
        HasSentBlockingRoadWarning = false;
    }

    public void UpdateSplinePoint()
    {
        if (EntryCar.Server.TrafficMap != null)
        {
            (CurrentSplinePoint, CurrentSplinePointDistanceSquared) = EntryCar.Server.TrafficMap.WorldToSpline(EntryCar.Status.Position);
        }
    }
}
