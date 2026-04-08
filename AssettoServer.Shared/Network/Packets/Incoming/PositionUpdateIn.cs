using System.Numerics;
using System.Runtime.InteropServices;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Utils;

namespace AssettoServer.Shared.Network.Packets.Incoming;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct PositionUpdateIn : IOutgoingNetworkPacket
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

    public PositionUpdateIn(byte pakSequenceId,
        uint lastRemoteTimestamp,
        Vector3 position,
        Vector3 rotation,
        Vector3 velocity,
        byte tyreAngularSpeedFl,
        byte tyreAngularSpeedFr,
        byte tyreAngularSpeedRl,
        byte tyreAngularSpeedRr,
        byte steerAngle,
        byte wheelAngle,
        ushort engineRpm,
        byte gear,
        CarStatusFlags statusFlag,
        short performanceDelta,
        byte gas,
        float normalizedPosition)
    {
        PakSequenceId = pakSequenceId;
        LastRemoteTimestamp = lastRemoteTimestamp;
        Position = position;
        Rotation = rotation;
        Velocity = velocity;
        TyreAngularSpeedFL = tyreAngularSpeedFl;
        TyreAngularSpeedFR = tyreAngularSpeedFr;
        TyreAngularSpeedRL = tyreAngularSpeedRl;
        TyreAngularSpeedRR = tyreAngularSpeedRr;
        SteerAngle = steerAngle;
        WheelAngle = wheelAngle;
        EngineRpm = engineRpm;
        Gear = gear;
        StatusFlag = statusFlag;
        PerformanceDelta = performanceDelta;
        Gas = gas;
        NormalizedPosition = normalizedPosition;
    }

    // Packets like this can crash the physics thread of other players
    public bool IsValid()
    {
        return !Position.ContainsNaN() && !Rotation.ContainsNaN() && !Velocity.ContainsNaN()
               && !Position.ContainsAbsLargerThan(100_000.0f) && !Velocity.ContainsAbsLargerThan(500.0f);
    }
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write(ACServerProtocol.PositionUpdate);
        var span = MemoryMarshal.CreateReadOnlySpan(in this, 1);
        writer.WriteBytes(MemoryMarshal.Cast<PositionUpdateIn, byte>(span));
    }
}
