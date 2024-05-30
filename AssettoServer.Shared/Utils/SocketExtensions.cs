using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace AssettoServer.Shared.Utils;

public static class SocketExtensions
{
    public static void DisableUdpIcmpExceptions(this Socket socket)
    {
        // https://stackoverflow.com/questions/5199026/c-sharp-async-udp-listener-socketexception
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            socket.IOControl(-1744830452 /* SIO_UDP_CONNRESET */, [0, 0, 0, 0], null);
        }
    }
}
