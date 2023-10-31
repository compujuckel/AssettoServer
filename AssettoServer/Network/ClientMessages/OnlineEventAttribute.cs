using System;

namespace AssettoServer.Network.ClientMessages;

[AttributeUsage(AttributeTargets.Class)]
public class OnlineEventAttribute : Attribute
{
    public string? Key { get; set; }
}
