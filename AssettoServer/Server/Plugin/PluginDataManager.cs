namespace AssettoServer.Server.Plugin;

public class PluginDataManager
{
    /// <summary>
    /// Fires when a shared event is initiated through a plugin.
    /// e.g. A player gets a new PB time or finishes a race challenge.
    /// </summary>
    public event EventHandler<EntryCar, PluginDataEventArgs>? PluginEvent;
    
    public void SendPluginEvent(EntryCar entryCar, PluginDataEventArgs eventArgs)
        => PluginEvent?.Invoke(entryCar, eventArgs);
}

public enum PluginDataType
{
    Points,
    Time,
    EventWin
}
