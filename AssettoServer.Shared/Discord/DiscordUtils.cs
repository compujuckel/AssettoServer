using System.Text.RegularExpressions;

namespace AssettoServer.Shared.Discord;

public static class DiscordUtils
{
    private static readonly string[] SensitiveCharacters = { "\\", "*", "_", "~", "`", "|", ">", ":", "@" };
    // https://discord.com/developers/docs/resources/webhook#create-webhook
    private static readonly string[] ForbiddenUsernameSubstrings = { "clyde", "discord", "@", "#", ":", "```" };
    private static readonly string[] ForbiddenUsernames = { "everyone", "here" };
    
    public static string Sanitize(string? text)
    {
        text ??= "";

        foreach (string unsafeChar in SensitiveCharacters)
        {
            text = text.Replace(unsafeChar, $"\\{unsafeChar}");
        }

        return text;
    }

    public static string SanitizeUsername(string? name)
    {
        name ??= "";

        foreach (string str in ForbiddenUsernames)
        {
            if (name == str) return $"_{str}";
        }

        foreach (string str in ForbiddenUsernameSubstrings)
        {
            name = Regex.Replace(name, str, new string('*', str.Length), RegexOptions.IgnoreCase);
        }

        name = name.Substring(0, Math.Min(name.Length, 80));

        return name;
    }
}
