using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Text;
using AssettoServer.Network.Packets.Outgoing;
using Serilog;

namespace AssettoServer.Server.Configuration;

// https://github.com/ac-custom-shaders-patch/acc-extension-config/wiki/Misc-%E2%80%93-Server-extra-options
public class CSPServerExtraOptions
{
    private static readonly string CspConfigSeparator = RepeatString("\t", 32) + "$CSP0:";

    private bool _hasShownMessageLengthWarning = false;

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

    public CSPServerExtraOptions(ACServerConfiguration configuration) : this(configuration.WelcomeMessage)
    {
        WelcomeMessage += LegalNotice.WelcomeMessage;
        if (configuration.Extra.EnableCustomUpdate)
        {
            ExtraOptions += "\r\n" + $"[EXTRA_DATA]\r\nCUSTOM_UPDATE_FORMAT = '{CSPPositionUpdate.CustomUpdateFormat}'";
        }
        ExtraOptions += "\r\n" + configuration.CSPExtraOptions;
    }

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

    [MemberNotNull(nameof(EncodedWelcomeMessage))]
    private void Encode()
    {
        EncodedWelcomeMessage = BuildWelcomeMessage();
        
        if (!_hasShownMessageLengthWarning && EncodedWelcomeMessage.Length > 2039)
        {
            _hasShownMessageLengthWarning = true;
            Log.Warning("Long welcome message detected. This will lead to crashes on CSP versions older than 0.1.77");
        }
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
        using (var d = new ZLibStream(m, CompressionLevel.SmallestSize))
        {
            d.Write(data);
        }
        
        return m.ToArray();
    }
    
    private static string DecompressZlib(byte[] data)
    {
        using var output = new MemoryStream();
        using var input = new MemoryStream(data);
        using (var d = new ZLibStream(input, CompressionMode.Decompress))
        {
            d.CopyTo(output);
        }
        
        byte[] bytes = output.ToArray();
        return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
    }
}
