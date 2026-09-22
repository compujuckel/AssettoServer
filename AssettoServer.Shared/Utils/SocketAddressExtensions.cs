using System.Net;
using System.Runtime.CompilerServices;

namespace AssettoServer.Shared.Utils;

public static class SocketAddressExtensions
{
    extension(SocketAddress address)
    {
        public SocketAddress Clone()
        {
            var clone = new SocketAddress(address.Family, address.Size);
            address.Buffer.CopyTo(clone.Buffer);
            return clone;
        }

        public bool IpEquals(SocketAddress other)
        {
            return address.GetIPv4Address() == other.GetIPv4Address();
        }

        public uint GetIPv4Address()
        {
            return GetIPv4Address(null, address.Buffer.Span);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod)]
    private static extern uint GetIPv4Address([UnsafeAccessorType("System.Net.SocketAddressPal, System.Net.Primitives")] object? a, ReadOnlySpan<byte> buffer);
}
