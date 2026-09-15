using System.Net;

namespace AssettoServer.Shared.Utils;

public static class IPAddressExtensions
{
    extension(IPAddress ip)
    {
        public string Redact(bool redact)
        {
            if (!redact)
                return ip.ToString();
        
            var privacyIp = ip.GetAddressBytes();
            privacyIp[3] = 0;
        
            return new IPAddress(privacyIp).ToString();
        }
    }
    
    extension(IPEndPoint ip)
    {
        public string Redact(bool redact)
        {
            return $"{ip.Address.Redact(redact)}:{ip.Port}";
        }
    }
}
