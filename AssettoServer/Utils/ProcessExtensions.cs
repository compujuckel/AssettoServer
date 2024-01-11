using System.Diagnostics;
using System.Reflection;

namespace AssettoServer.Utils;

public static class ProcessExtensions
{
    public static int GetParentProcessId(this Process process)
    {
        return (int)typeof(Process)
            .GetProperty("ParentProcessId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(process)!;
    }
}
