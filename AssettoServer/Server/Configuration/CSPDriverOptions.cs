using System;
using AssettoServer.Shared.Model;

namespace AssettoServer.Server.Configuration;

public static class CSPDriverOptions
{
    public static DriverOptionsFlags Parse(string? skin)
    {
        if (skin == null)
            return default;
        
        int separatorPos = skin.LastIndexOf('/');
        if (separatorPos > 0)
        {
            string packed = skin.Substring(separatorPos + 1);
            byte[] unpacked = Convert.FromBase64String(packed.PadRight(4 * ((packed.Length + 3) / 4), '='));

            if (unpacked.Length == 3 && unpacked[0] == 0 && unpacked[2] == (byte)(unpacked[1] ^ 0x17))
            {
                return (DriverOptionsFlags)unpacked[1];
            }
        }

        return default;
    }
}
