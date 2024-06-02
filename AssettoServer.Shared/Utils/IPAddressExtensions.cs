using System.Net;

namespace AssettoServer.Shared.Utils;

public static class IPAddressExtensions
{
    public static string Redact(this IPAddress ip, bool usePrivacyMode)
    {
        if (!usePrivacyMode)
            return ip.ToString();
        
        var privacyIp = ip.GetAddressBytes();
        privacyIp[3] = 0;
        
        return new IPAddress(privacyIp).ToString();
    }
    
    public static string Redact(this IPEndPoint ip, bool usePrivacyMode)
    {
        return $"{ip.Address.Redact(usePrivacyMode)}:{ip.Port}";
    }
}
