using System.Collections.Generic;

namespace AssettoServer.Network.Http
{
    public class PlayerListResponse
    {
        public List<PlayerListEntry> Players { get; init; }
    }

    public class PlayerListEntry
    {
        public int SessionId { get; init; }
        public string Guid { get; init; }
        public string Name { get; init; }
        public string Country { get; init; }
        public string Car { get; init; }
        public string Skin { get; init; }
    }
}