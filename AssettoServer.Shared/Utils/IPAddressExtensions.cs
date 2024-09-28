using System.Net;

namespace AssettoServer.Shared.Utils;

public static class IPAddressExtensions
{
    public static string Redact(this IPAddress ip, bool redact)
    {
        if (!redact)
            return ip.ToString();
        
        var privacyIp = ip.GetAddressBytes();
        privacyIp[3] = 0;
        
        return new IPAddress(privacyIp).ToString();
    }
    
    public static string Redact(this IPEndPoint ip, bool redact)
    {
        return $"{ip.Address.Redact(redact)}:{ip.Port}";
    }
}
