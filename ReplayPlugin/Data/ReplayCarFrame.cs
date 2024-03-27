using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AssettoServer.Shared.Model;
using AssettoServer.Shared.Network.Packets.Outgoing;
using ReplayPlugin.Utils;

namespace ReplayPlugin.Data;

public struct ReplayCarFrame
{
    public Vector3 WorldTranslation;
    public Vector3h WorldOrientation;
    public ushort Status;
    public Vector3h Velocity;
    public Half EngineRpm;
    public HalfArray4 WheelAngularSpeed;
    public Half Steer;
    public byte Gear;
    public byte Gas;
    public byte Brake;
    public byte SessionId;
    public short AiMappingStartIndex;

    public static ReplayCarFrame FromCarStatus(byte sessionId, CarStatus status)
    {
        return new ReplayCarFrame
        {
            SessionId = sessionId,
            WorldTranslation = status.Position,
            WorldOrientation = new Vector3h(status.Rotation),
            EngineRpm = (Half)status.EngineRpm,
            Gas = status.Gas,
            Brake = (byte)(status.StatusFlag.HasFlag(CarStatusFlags.BrakeLightsOn) ? 255 : 0),
            Gear = status.Gear,
            Velocity = new Vector3h(status.Velocity),
            AiMappingStartIndex = -1
        };
    }
    
    public void ToWriter(BinaryWriter writer, bool connected)
    {
        writer.WriteStruct(new KunosReplayCarFrame
        {
            WorldTranslation = WorldTranslation,
            WorldOrientation = WorldOrientation,
            Velocity = Velocity,
            EngineRpm = EngineRpm,
            WheelAngularSpeed = WheelAngularSpeed,
            Steer = Steer,
            Gear = Gear,
            Gas = Gas,
            Brake = Brake,
            Connected = connected
        });
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vector3h
{
    public Half X;
    public Half Y;
    public Half Z;

    public Vector3h(Vector3 vec)
    {
        X = (Half)vec.X;
        Y = (Half)vec.Y;
        Z = (Half)vec.Z;
    }
}

[InlineArray(4)]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct HalfArray4
{
    private Half _element;
}
