using System.Numerics;
using System.Runtime.InteropServices;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.Incoming;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct PositionUpdateIn
{
    public readonly byte PakSequenceId;
    public readonly uint LastRemoteTimestamp;
    public readonly Vector3 Position;
    public readonly Vector3 Rotation;
    public readonly Vector3 Velocity;
    public readonly byte TyreAngularSpeedFL;
    public readonly byte TyreAngularSpeedFR;
    public readonly byte TyreAngularSpeedRL;
    public readonly byte TyreAngularSpeedRR;
    public readonly byte SteerAngle;
    public readonly byte WheelAngle;
    public readonly ushort EngineRpm;
    public readonly byte Gear;
    public readonly CarStatusFlags StatusFlag;
    public readonly short PerformanceDelta;
    public readonly byte Gas;
    public readonly float NormalizedPosition;

    public bool IsValid()
    {
        return !(float.IsNaN(Position.X) || float.IsNaN(Position.Y) || float.IsNaN(Position.Z)
               || float.IsNaN(Rotation.X) || float.IsNaN(Rotation.Y) || float.IsNaN(Rotation.Z)
               || float.IsNaN(Velocity.X) || float.IsNaN(Velocity.Y) || float.IsNaN(Velocity.Z));
    }
}
