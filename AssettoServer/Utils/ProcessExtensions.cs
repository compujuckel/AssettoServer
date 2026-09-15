using System.Diagnostics;
using System.Reflection;

namespace AssettoServer.Utils;

public static class ProcessExtensions
{
    extension(Process process)
    {
        public int GetParentProcessId()
        {
            return (int)typeof(Process)
                .GetProperty("ParentProcessId", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(process)!;
        }
    }
}
