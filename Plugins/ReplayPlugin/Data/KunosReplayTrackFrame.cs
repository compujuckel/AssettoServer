using System.Runtime.InteropServices;

namespace ReplayPlugin.Data;

[StructLayout(LayoutKind.Explicit, Size = 4)]
public struct KunosReplayTrackFrame
{
    [FieldOffset(0)] public Half SunAngle;
    [FieldOffset(2)] public Half Unknown;
}
