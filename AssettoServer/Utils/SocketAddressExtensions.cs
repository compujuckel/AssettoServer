using System;
using System.Net;

namespace AssettoServer.Utils;

public static class SocketAddressExtensions
{
    public static SocketAddress Clone(this SocketAddress address)
    {
        var clone = new SocketAddress(address.Family, address.Size);
        address.Buffer.CopyTo(clone.Buffer);
        return clone;
    }

    public static bool IpEquals(this SocketAddress address, SocketAddress other)
    {
        // This works for IPv4 only. First two bytes = port, next 4 bytes = address
        return address.Buffer.Span[2..6].SequenceEqual(other.Buffer.Span[2..6]);
    }
}
