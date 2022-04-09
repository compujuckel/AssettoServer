using AssettoServer.Server;

namespace AutoModerationPlugin;

internal class EntryCarAutoModeration
{
    public EntryCar EntryCar { get; }

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
}
