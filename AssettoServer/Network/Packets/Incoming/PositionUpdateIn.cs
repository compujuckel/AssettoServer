using System.Numerics;
using System.Runtime.InteropServices;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Utils;

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
    
    // Packets like this can crash the physics thread of other players
    public bool IsValid()
    {
        return !Position.ContainsNaN() && !Rotation.ContainsNaN() && !Velocity.ContainsNaN()
               && !Position.ContainsAbsLargerThan(100_000.0f) && !Velocity.ContainsAbsLargerThan(500.0f);
    }
}
