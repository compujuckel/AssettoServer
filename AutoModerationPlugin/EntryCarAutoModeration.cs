using AssettoServer.Server;
using AssettoServer.Server.Ai;
using AutoModerationPlugin.Packets;

namespace AutoModerationPlugin;

public class EntryCarAutoModeration
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
    
    public Flags CurrentFlags { get; set; }

    private readonly TrafficMap? _trafficMap;
    
    public EntryCarAutoModeration(EntryCar entryCar, TrafficMap? trafficMap = null)
    {
        EntryCar = entryCar;
        EntryCar.ResetInvoked += OnResetInvoked;
        _trafficMap = trafficMap;
    }

    private void OnResetInvoked(EntryCar sender, EventArgs args)
    {
        NoLightSeconds = 0;
        HasSentNoLightWarning = false;
        WrongWaySeconds = 0;
        HasSentNoLightWarning = false;
        BlockingRoadSeconds = 0;
        HasSentBlockingRoadWarning = false;
        CurrentFlags = 0;
    }

    public void UpdateSplinePoint()
    {
        if (_trafficMap != null)
        {
            (CurrentSplinePoint, CurrentSplinePointDistanceSquared) = _trafficMap.WorldToSpline(EntryCar.Status.Position);
        }
    }
}
