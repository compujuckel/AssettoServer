using System.Net;

namespace AssettoServer.Shared.Utils;

public static class IPEndpointUtils
{
    public static IPEndPoint FromSocketAddress(SocketAddress address)
    {
        var tmp = new IPEndPoint(0, 0);
        return (IPEndPoint)tmp.Create(address);
    }
}
