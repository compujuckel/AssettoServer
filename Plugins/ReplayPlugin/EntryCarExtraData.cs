using System.Numerics;

namespace ReplayPlugin;

public class EntryCarExtraData
{
    public static readonly EntryCarExtraData Empty = new();

    public Vector3[] WheelPositions = new Vector3[4];
}
