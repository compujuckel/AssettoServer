using System.Collections.Generic;

namespace AssettoServer.Network.ClientMessages;

internal class OnlineEventInfo
{
    public string? Key { get; init; }
    public bool Udp { get; init; }
    public required List<OnlineEventFieldInfo> Fields { get; init; }
    public uint PacketType { get; init; }
    public required string Structure { get; init; }
}
