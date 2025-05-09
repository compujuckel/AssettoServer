using AssettoServer.Network.ClientMessages;

namespace TougePlugin.Packets;

[OnlineEvent(Key = "AS_LobbyStatus")]
public class LobbyStatusPacket : OnlineEvent<LobbyStatusPacket>
{
    // Details for nearby player
    [OnlineEventField(Name = "nearbyName1", Size = 32)]
    public string NearbyPlayerName1;
    [OnlineEventField(Name = "nearbyId1")]
    public ulong NearbyPlayerId1;
    [OnlineEventField(Name = "nearbyInRace1")]
    public bool NearbyPlayerInRace1;

    [OnlineEventField(Name = "nearbyName2", Size = 32)]
    public string NearbyPlayerName2;
    [OnlineEventField(Name = "nearbyId2")]
    public ulong NearbyPlayerId2;
    [OnlineEventField(Name = "nearbyInRace2")]
    public bool NearbyPlayerInRace2;

    [OnlineEventField(Name = "nearbyName3", Size = 32)]
    public string NearbyPlayerName3;
    [OnlineEventField(Name = "nearbyId3")]
    public ulong NearbyPlayerId3;
    [OnlineEventField(Name = "nearbyInRace3")]
    public bool NearbyPlayerInRace3;

    [OnlineEventField(Name = "nearbyName4", Size = 32)]
    public string NearbyPlayerName4;
    [OnlineEventField(Name = "nearbyId4")]
    public ulong NearbyPlayerId4;
    [OnlineEventField(Name = "nearbyInRace4")]
    public bool NearbyPlayerInRace4;

    [OnlineEventField(Name = "nearbyName5", Size = 32)]
    public string NearbyPlayerName5;
    [OnlineEventField(Name = "nearbyId5")]
    public ulong NearbyPlayerId5;
    [OnlineEventField(Name = "nearbyInRace5")]
    public bool NearbyPlayerInRace5;
}
