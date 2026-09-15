using System.Numerics;

namespace AssettoServer.Shared.Utils;

public static class Vector3Extensions
{
    extension(Vector3 vec)
    {
        public bool ContainsNaN()
        {
            return float.IsNaN(vec.X) || float.IsNaN(vec.Y) || float.IsNaN(vec.Z);
        }

        public bool ContainsAbsLargerThan(float val)
        {
            return MathF.Abs(vec.X) > val || MathF.Abs(vec.Y) > val || MathF.Abs(vec.Z) > val;
        }
    }
}
