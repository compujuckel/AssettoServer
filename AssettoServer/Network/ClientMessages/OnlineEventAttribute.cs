using System;
using JetBrains.Annotations;

namespace AssettoServer.Network.ClientMessages;

[AttributeUsage(AttributeTargets.Class)]
[BaseTypeRequired(typeof(OnlineEvent<>))]
public class OnlineEventAttribute : Attribute
{
    public string? Key { get; set; }
    public bool Udp { get; set; }
}
