using System;
using JetBrains.Annotations;

namespace AssettoServer.Network.ClientMessages;

[AttributeUsage(AttributeTargets.Field)]
[MeansImplicitUse]
public class OnlineEventFieldAttribute : Attribute
{
    public string? Name { get; set; }
    public int Size { get; set; }
}
