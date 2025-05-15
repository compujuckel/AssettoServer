using System;
using System.Reflection;

namespace AssettoServer;

internal static class ThisAssembly
{
    internal static string AssemblyInformationalVersion { get; }
    internal static string? GitCommitHash { get; }

    static ThisAssembly()
    {
        var informationalVersion = GetAssemblyAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        var plusIndex = informationalVersion.IndexOf('+');
        if (plusIndex > 0)
        {
            AssemblyInformationalVersion = informationalVersion[..plusIndex];
            GitCommitHash = informationalVersion[(plusIndex + 1)..];
        }
        else
        {
            AssemblyInformationalVersion = informationalVersion;
        }
    }

    private static T GetAssemblyAttribute<T>() where T : Attribute =>
        (T)Attribute.GetCustomAttribute(typeof(ThisAssembly).Assembly, typeof(T), false)!;
}
