using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace AssettoServer.Shared.Utils;

// https://github.com/ac-custom-shaders-patch/acc-extension-config/wiki/Misc-%E2%80%93-Server-extra-options
public static class CSPServerExtraOptionsParser
{
    private const string CspConfigPattern = @"\t+\$CSP0:([^\s]+)";
    private static readonly string CspConfigSeparator = RepeatString("\t", 32) + "$CSP0:";
    
    public static (string WelcomeMessage, string ExtraOptions) Decode(string? welcomeMessage)
    {
        welcomeMessage ??= "";
        
        var match = Regex.Match(welcomeMessage, CspConfigPattern);
        if (match.Success)
        {
            string extraOptionsEncoded = match.Groups[1].Value;
            return (welcomeMessage[..match.Groups[0].Index], DecompressZlib(Convert.FromBase64String(extraOptionsEncoded.PadRight(4 * ((extraOptionsEncoded.Length + 3) / 4), '='))));
        }

        return (welcomeMessage, "");
    }

    public static string Encode(string welcomeMessage, string? extraOptions) {
        if (string.IsNullOrWhiteSpace(extraOptions)) return welcomeMessage;
        return $"{welcomeMessage}{CspConfigSeparator}{ToCutBase64(CompressZlib(Encoding.UTF8.GetBytes(extraOptions)))}";
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
