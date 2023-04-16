using System;
using System.Numerics;

namespace AssettoServer.Utils;

public static class Vector3Extensions
{
    public static bool ContainsNaN(this Vector3 vec)
    {
        return float.IsNaN(vec.X) || float.IsNaN(vec.Y) || float.IsNaN(vec.Z);
    }

    public static bool ContainsAbsLargerThan(this Vector3 vec, float val)
    {
        return MathF.Abs(vec.X) > val || MathF.Abs(vec.Y) > val || MathF.Abs(vec.Z) > val;
    }
}
