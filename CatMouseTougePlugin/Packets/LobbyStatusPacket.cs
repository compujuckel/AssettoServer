using AssettoServer.Network.ClientMessages;

namespace CatMouseTougePlugin.Packets;

[OnlineEvent(Key = "AS_LobbyStatus")]
public class LobbyStatusPacket : OnlineEvent<LobbyStatusPacket>
{
    // Details for nearby player
    [OnlineEventField(Name = "nearbyName", Size = 32)]
    public string NearbyPlayerName;
    [OnlineEventField(Name = "nearbyId")]
    public ulong NearbyPlayerId;
    [OnlineEventField(Name = "nearbyInRace")]
    public bool NearbyPlayerInRace;

    //// Details for all connected players
    //[OnlineEventField(Name = "connectedIds", Size = 32)]
    //public int[] ConnectedIds;
    //[OnlineEventField(Name = "connectedInRaces", Size = 32)]
    //public bool[] ConnectedInRaces;
}
