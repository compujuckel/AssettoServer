using System.Net;
using System.Net.Sockets;

namespace AssettoServer.Shared.Utils;

public static class IPAddressExtensions
{
    public static string ToGdprString(this IPAddress ip, bool useGdpr)
    {
        if (!useGdpr)
            return ip.ToString();
        
        var gdprIp = ip.GetAddressBytes();
        gdprIp[3] = 0;
        
        return new IPAddress(gdprIp).ToString();
    }
    
    public static string ToGdprString(this IPEndPoint ip, bool useGdpr)
    {
        return $"{ip.Address.ToGdprString(useGdpr)}:{ip.Port}";
    }
}
