using System.Net;
using System.Reflection;

namespace AssettoServer.Shared.Utils;

public static class SocketAddressExtensions
{
    private static readonly GetIPv4AddressMethod GetIPv4AddressDelegate;
    private delegate uint GetIPv4AddressMethod(ReadOnlySpan<byte> buffer);
    
    public static SocketAddress Clone(this SocketAddress address)
    {
        var clone = new SocketAddress(address.Family, address.Size);
        address.Buffer.CopyTo(clone.Buffer);
        return clone;
    }

    public static bool IpEquals(this SocketAddress address, SocketAddress other)
    {
        return address.GetIPv4Address() == other.GetIPv4Address();
    }

    public static uint GetIPv4Address(this SocketAddress address)
    {
        return GetIPv4AddressDelegate(address.Buffer.Span);
    }

    static SocketAddressExtensions()
    {
        GetIPv4AddressDelegate = Assembly.GetAssembly(typeof(SocketAddress))!
            .GetType("System.Net.SocketAddressPal")!
            .GetMethod("GetIPv4Address", BindingFlags.Public | BindingFlags.Static)!
            .CreateDelegate<GetIPv4AddressMethod>();
    }
}
