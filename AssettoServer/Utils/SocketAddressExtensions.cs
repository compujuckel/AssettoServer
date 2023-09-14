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
}
