using System.Net;
using System.Net.Sockets;

namespace AssettoServer.Shared.Utils;

public static class IPAddressExtensions
{
    public static string ToPrivacyString(this IPAddress ip, bool usePrivacyMode)
    {
        if (!usePrivacyMode)
            return ip.ToString();
        
        var privacyIp = ip.GetAddressBytes();
        privacyIp[3] = 0;
        
        return new IPAddress(privacyIp).ToString();
    }
    
    public static string ToPrivacyString(this IPEndPoint ip, bool usePrivacyMode)
    {
        return $"{ip.Address.ToPrivacyString(usePrivacyMode)}:{ip.Port}";
    }
}
