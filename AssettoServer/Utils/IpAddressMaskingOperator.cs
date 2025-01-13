using System.Text.RegularExpressions;
using Serilog.Enrichers.Sensitive;

namespace AssettoServer.Utils;

public partial class IpAddressMaskingOperator : IMaskingOperator
{
    [GeneratedRegex(@"(\d{1,3}\.\d{1,3}\.\d{1,3})\.\d{1,3}")]
    private static partial Regex IpAddressRegex();
    
    public MaskingResult Mask(string input, string mask)
    {
        var result = IpAddressRegex().Replace(input, "$1.0");

        return new MaskingResult
        {
            Result = result,
            Match = result != input
        };
    }
}
