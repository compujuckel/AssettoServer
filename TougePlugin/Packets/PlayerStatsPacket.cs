using AssettoServer.Network.ClientMessages;

namespace TougePlugin.Packets;

[OnlineEvent(Key = "AS_PlayerStats")]
public class PlayerStatsPacket : OnlineEvent<PlayerStatsPacket>
{
    [OnlineEventField(Name = "elo")]
    public int Elo = 1000;

    [OnlineEventField(Name = "racesCompleted")]
    public int RacesCompleted = 0;
}
