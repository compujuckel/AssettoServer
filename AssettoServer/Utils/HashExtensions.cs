using System.Diagnostics.CodeAnalysis;

namespace AssettoServer.Utils;

public static class HashExtensions
{
    /// <summary>
    /// https://github.com/gro-ove/actools/blob/259206255d0ffc2122e70933ed1ba64bc9cbfaef/AcTools/Utils/Helpers/ArrayExtension.cs#L76-L86
    ///
    /// I really don't want to bother with writing this from scratch
    /// </summary>
    public static string ToHexString([NotNull] this byte[] data) {
        const string lookup = "0123456789ABCDEF";
        int i = -1, p = -1, l = data.Length;
        var c = new char[l-- * 2];
        while (i < l) {
            var d = data[++i];
            c[++p] = lookup[d >> 4];
            c[++p] = lookup[d & 0xF];
        }
        return new string(c, 0, c.Length);
    }
}
