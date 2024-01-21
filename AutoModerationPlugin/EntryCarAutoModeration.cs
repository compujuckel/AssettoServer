using AssettoServer.Server;
using AssettoServer.Server.Ai.Splines;
using AutoModerationPlugin.Packets;

namespace AutoModerationPlugin;

public class EntryCarAutoModeration
{
    public EntryCar EntryCar { get; }

    public int CurrentSplinePointId { get; private set; } = -1;
    public float CurrentSplinePointDistanceSquared { get; private set; }
    
    public int HighPingSeconds { get; set; }
    public bool HasSentHighPingWarning { get; set; }

    public int NoLightSeconds { get; set; }
    public bool HasSentNoLightWarning { get; set; }
    public int NoLightsPitCount { get; set; }

    public int WrongWaySeconds { get; set; }
    public bool HasSentWrongWayWarning { get; set; }
    public int WrongWayPitCount { get; set; }
    
    public int BlockingRoadSeconds { get; set; }
    public bool HasSentBlockingRoadWarning { get; set; }
    public int BlockingRoadPitCount { get; set; }
    
    public Flags CurrentFlags { get; set; }

    private readonly AiSpline? _aiSpline;
    
    public EntryCarAutoModeration(EntryCar entryCar, AiSpline? aiSpline = null)
    {
        EntryCar = entryCar;
        EntryCar.ResetInvoked += OnResetInvoked;
        _aiSpline = aiSpline;
    }

    private void OnResetInvoked(EntryCar sender, EventArgs args)
    {
        HighPingSeconds = 0;
        HasSentHighPingWarning = false;
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
        if (_aiSpline != null)
        {
            (CurrentSplinePointId, CurrentSplinePointDistanceSquared) = _aiSpline.WorldToSpline(EntryCar.Status.Position);
        }
    }
}
