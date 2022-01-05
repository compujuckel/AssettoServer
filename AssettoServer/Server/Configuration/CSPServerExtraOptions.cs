using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace AssettoServer.Server.Configuration;

// https://github.com/ac-custom-shaders-patch/acc-extension-config/wiki/Misc-%E2%80%93-Server-extra-options
public class CSPServerExtraOptions
{
    private static readonly string CspConfigSeparator = RepeatString("\t", 32) + "$CSP0:";

    public string WelcomeMessage
    {
        get => _welcomeMessage;
        set
        {
            _welcomeMessage = value;
            Encode();
        }
    }

    public string ExtraOptions
    {
        get => _extraOptions;
        set
        {
            _extraOptions = value;
            Encode();
        }
    }
    public string EncodedWelcomeMessage { get; private set; }

    private string _welcomeMessage = "";
    private string _extraOptions = "";

    public CSPServerExtraOptions(string welcomeMessage)
    {
        Decode(welcomeMessage);
        Encode();
    }

    private void Decode(string welcomeMessage)
    {
        int pos = welcomeMessage.IndexOf(CspConfigSeparator, StringComparison.Ordinal);
        if (pos > 0)
        {
            string extraOptionsEncoded = welcomeMessage.Substring(pos + CspConfigSeparator.Length);
            
            _welcomeMessage = welcomeMessage.Substring(0, pos);
            _extraOptions = DecompressZlib(Convert.FromBase64String(extraOptionsEncoded.PadRight(4*((extraOptionsEncoded.Length+3)/4), '=')));
        }
        else
        {
            _welcomeMessage = welcomeMessage;
        }
    }

    private void Encode()
    {
        EncodedWelcomeMessage = BuildWelcomeMessage();
    }
    
    private string BuildWelcomeMessage() {
        if (string.IsNullOrWhiteSpace(_extraOptions)) return _welcomeMessage;
        return _welcomeMessage + CspConfigSeparator 
                               + ToCutBase64(CompressZlib(Encoding.UTF8.GetBytes(_extraOptions)));
    }

    private static string RepeatString(string s, int number) {
        var b = new StringBuilder();
        for (var i = 0; i < number; i++) {
            b.Append(s);
        }
        return b.ToString();
    }

    private static string ToCutBase64(byte[] decoded) {
        return Convert.ToBase64String(decoded).TrimEnd('=');
    }

    private static byte[] CompressZlib(byte[] data)
    {
        using var m = new MemoryStream();
        using (var d = new ZLibStream(m, CompressionLevel.SmallestSize)) {
            d.Write(data);
        }
            
        return m.ToArray();
    }
    
    private static string DecompressZlib(byte[] data)
    {
        var output = new byte[1024];
        
        using var m = new MemoryStream(data);
        using (var d = new ZLibStream(m, CompressionMode.Decompress))
        {
            int count = d.Read(output, 0, output.Length);
            return Encoding.UTF8.GetString(output, 0, count);
        }
    }
}