using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace AssettoServer.Shared.Utils;

// https://github.com/ac-custom-shaders-patch/acc-extension-config/wiki/Misc-%E2%80%93-Server-extra-options
public static partial class CSPServerExtraOptionsParser
{
    [GeneratedRegex(@"\t+\$CSP0:([^\s]+)")]
    private static partial Regex CspConfigRegex();
    private static readonly string CspConfigSeparator = RepeatString("\t", 32) + "$CSP0:";
    
    public static (string WelcomeMessage, string ExtraOptions) Decode(string? welcomeMessage)
    {
        welcomeMessage ??= "";
        
        var match = CspConfigRegex().Match(welcomeMessage);
        if (match.Success)
        {
            string extraOptionsEncoded = match.Groups[1].Value;
            return (welcomeMessage[..match.Groups[0].Index], DecompressZlib(Convert.FromBase64String(extraOptionsEncoded.PadRight(4 * ((extraOptionsEncoded.Length + 3) / 4), '='))));
        }

        return (welcomeMessage, "");
    }

    public static string Encode(string welcomeMessage, string? extraOptions)
    {
        return string.IsNullOrWhiteSpace(extraOptions)
            ? welcomeMessage 
            : $"{welcomeMessage}{CspConfigSeparator}{ToCutBase64(CompressZlib(Encoding.UTF8.GetBytes(extraOptions)).Span)}";
    }

    private static string RepeatString(string s, int number) {
        var b = new StringBuilder();
        for (var i = 0; i < number; i++) {
            b.Append(s);
        }
        return b.ToString();
    }

    private static string ToCutBase64(ReadOnlySpan<byte> decoded) {
        return Convert.ToBase64String(decoded).TrimEnd('=');
    }

    private static Memory<byte> CompressZlib(byte[] data)
    {
        using var m = new MemoryStream();
        using (var d = new ZLibStream(m, CompressionLevel.Optimal, true))
        {
            d.Write(data);
        }
        
        return m.GetBuffer().AsMemory(0, (int)m.Position);
    }
    
    private static string DecompressZlib(byte[] data)
    {
        using var output = new MemoryStream();
        using var input = new MemoryStream(data);
        using (var d = new ZLibStream(input, CompressionMode.Decompress))
        {
            d.CopyTo(output);
        }
        
        return Encoding.UTF8.GetString(output.GetBuffer().AsSpan(0, (int)output.Position));
    }
}
