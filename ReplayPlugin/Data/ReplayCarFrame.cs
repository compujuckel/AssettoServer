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

    private static float DecodeAngularSpeed(float speed)
    {
        speed -= 100;
        var sign = Math.Sign(speed);
        return (MathF.Pow(10.0f, speed / sign / 20.0f) - 1.0f) * sign;
    }
    
    public static ReplayCarFrame FromCarStatus(byte sessionId, CarStatus status)
    {
        var angularSpeeds = new HalfArray4();
        for (int i = 0; i < 4; i++)
        {
            angularSpeeds[i] = (Half)DecodeAngularSpeed(status.TyreAngularSpeed[i]);
        }
        
        return new ReplayCarFrame
        {
            SessionId = sessionId,
            WorldTranslation = status.Position,
            WorldOrientation = new Vector3h(status.Rotation),
            EngineRpm = (Half)status.EngineRpm,
            Gas = status.Gas,
            Brake = (byte)(status.StatusFlag.HasFlag(CarStatusFlags.BrakeLightsOn) ? byte.MaxValue : 0),
            Gear = status.Gear,
            Velocity = new Vector3h(status.Velocity),
            WheelAngularSpeed = angularSpeeds,
            Steer = (Half)(status.SteerAngle - 127),
            AiMappingStartIndex = -1
        };
    }
    
    public void ToWriter(BinaryWriter writer, bool connected, EntryCarExtraData extra)
    {
        var frame = new KunosReplayCarFrame
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
            Connected = connected,
            EngineLife = byte.MaxValue,
            Fuel = 1
        };

        var rotationMatrix = Matrix4x4.CreateFromYawPitchRoll((float)-WorldOrientation.X, (float)-WorldOrientation.Y, (float)-WorldOrientation.Z);
        var translationMatrix = Matrix4x4.CreateTranslation(WorldTranslation);
        var transformationMatrix = rotationMatrix * translationMatrix;

        for (int i = 0; i < 4; i++)
        {
            var translation = Vector3.Transform(extra.WheelPositions[i], transformationMatrix);
            frame.SusTranslation[i] = translation;
            frame.TyreTranslation[i] = translation;

            frame.SusOrientation[i] = WorldOrientation;
            frame.TyreOrientation[i] = WorldOrientation;
        }

        writer.WriteStruct(in frame);
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
