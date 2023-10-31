using System;

namespace AssettoServer.Network.ClientMessages;

[AttributeUsage(AttributeTargets.Field)]
public class OnlineEventField : Attribute
{
    public string? Name { get; set; }
    public int Size { get; set; }
}
