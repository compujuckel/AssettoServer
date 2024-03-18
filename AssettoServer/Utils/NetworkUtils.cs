using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AssettoServer.Utils;

public static class NetworkUtils
{
    public static IPAddress GetPrimaryIpAddress()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Unspecified);
        // There is no traffic sent just by connecting the socket, so the target IP doesn't really matter (it must be a public IP though)
        socket.Connect("8.8.8.8", 65530);
        var endPoint = (IPEndPoint)socket.LocalEndPoint!;
        return endPoint.Address;
    }

    public static IPAddress? GetGatewayAddressForInterfaceWithIpAddress(IPAddress address)
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Select(i => i.GetIPProperties())
                .First(i => i.UnicastAddresses.Any(u => u.Address.Equals(address)))
                .GatewayAddresses.First(g => g.Address.AddressFamily == AddressFamily.InterNetwork).Address;
        }
        catch
        {
            return null;
        }
    }
}
