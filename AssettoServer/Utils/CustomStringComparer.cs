using System;
using System.Collections.Generic;

namespace AssettoServer.Utils;
public class CustomStringComparer : IComparer<string>
{
    public int Compare(string x, string y)
    {
        // Check if x or y is null
        if (x == null) return y == null ? 0 : -1;
        if (y == null) return 1;

        // Check if x or y is non-alphanumeric
        bool xIsNonAlphaNum = !char.IsLetterOrDigit(x[0]);
        bool yIsNonAlphaNum = !char.IsLetterOrDigit(y[0]);

        if (xIsNonAlphaNum && !yIsNonAlphaNum)
        {
            return 1; // x is non-alphanumeric and should be below y
        }
        else if (!xIsNonAlphaNum && yIsNonAlphaNum)
        {
            return -1; // y is non-alphanumeric and should be below x
        }
        else
        {
            // Both are either alphanumeric or non-alphanumeric, so compare normally
            return string.Compare(x, y, StringComparison.Ordinal);
        }
    }
}
