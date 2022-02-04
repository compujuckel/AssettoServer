using System;
using System.Numerics;

namespace AssettoServer.Network.Packets.Outgoing;

[Flags]
public enum CarStatusFlags
{
    BrakeLightsOn = 0x10,
    LightsOn = 0x20,
    Horn = 0x40,
    HazardsOn = 0x2000,
    HighBeamsOff = 0x4000,
    WiperLevel1 = 0x200000,
    WiperLevel2 = 0x400000,
    WiperLevel3 = WiperLevel1 | WiperLevel2
}

public readonly struct PositionUpdateOut : IOutgoingNetworkPacket
{
    public readonly byte SessionId;
    public readonly byte PakSequenceId;
    public readonly uint Timestamp;
    public readonly ushort Ping;
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

    public PositionUpdateOut(byte sessionId,
        byte pakSequenceId,
        uint timestamp,
        ushort ping,
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
        byte gas)
    {
        SessionId = sessionId;
        PakSequenceId = pakSequenceId;
        Timestamp = timestamp;
        Ping = ping;
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
    }

    public void ToWriter(ref PacketWriter writer) => ToWriter(ref writer, false);

    public void ToWriter(ref PacketWriter writer, bool batched)
    {
        if(!batched)
            writer.Write<byte>(0x46);
        writer.Write(SessionId);
        writer.Write(PakSequenceId);
        writer.Write(Timestamp);
        writer.Write(Ping);
        writer.Write(Position);
        writer.Write(Rotation);
        writer.Write(Velocity);
        writer.Write(TyreAngularSpeedFL);
        writer.Write(TyreAngularSpeedFR);
        writer.Write(TyreAngularSpeedRL);
        writer.Write(TyreAngularSpeedRR);
        writer.Write(SteerAngle);
        writer.Write(WheelAngle);
        writer.Write(EngineRpm);
        writer.Write(Gear);
        writer.Write((uint)StatusFlag);
        if (!batched)
        {
            writer.Write(PerformanceDelta);
            writer.Write(Gas);
        }
    }
}