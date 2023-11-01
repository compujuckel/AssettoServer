using System;
using System.Reflection;

namespace AssettoServer.Network.ClientMessages;

internal class OnlineEventFieldInfo
{
    public required string Name { get; init; }
    public required Type Type { get; init; }
    public required string DefType { get; init; }
    public int Size { get; init; }
    public int? Array { get; init; }
    public required FieldInfo Field { get; init; }
}
