using System.Runtime.InteropServices;

namespace AssettoServer.Shared.Utils;

public static class MarshalUtils
{
    // Marshal.SizeOf(typeof(bool)) returns 4 - we need 1. See https://stackoverflow.com/a/47956291
    public static int SizeOf(Type t)
    {
        return t == typeof(bool) ? 1 : Marshal.SizeOf(t);
    }
}
