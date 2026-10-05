using AssettoServer.Network.ClientMessages;

namespace VotingPresetPlugin.Preset;

[OnlineEvent(Key = "reconnectClient")]
public class ReconnectClientPacket : OnlineEvent<ReconnectClientPacket>
{
    
    /// <summary>
    /// Time in seconds.
    /// Client script should wait this amount of time.
    /// <para/>
    /// Yes, i'm looking at you custom plugin people, let people configure this.
    /// Would be great if people get a warning before the game just goes poof yk.
    /// </summary>
    [OnlineEventField(Name = "time")]
    public ushort Time = 0;
}
