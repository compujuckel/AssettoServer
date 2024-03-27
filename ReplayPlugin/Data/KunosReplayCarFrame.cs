using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ReplayPlugin.Data;

[StructLayout(LayoutKind.Explicit, Size = 276)]
public struct KunosReplayCarFrame
{
    [FieldOffset(0)]   public Vector3 BodyTranslation;
    [FieldOffset(12)]  public Vector3h BodyOrientation;
    
    [FieldOffset(20)]  public Vector3 WorldTranslation;
    [FieldOffset(32)]  public Vector3h WorldOrientation;
    
    [FieldOffset(40)]  public Vector3Array4 SusTranslation;
    [FieldOffset(88)]  public Vector3hArray4 SusOrientation;
    [FieldOffset(112)] public Vector3Array4 TyreTranslation;
    [FieldOffset(160)] public Vector3hArray4 TyreOrientation;
    [FieldOffset(184)] public Vector3h Velocity;
    [FieldOffset(190)] public Half EngineRpm;
    [FieldOffset(192)] public HalfArray4 WheelAngularSpeed;
    [FieldOffset(200)] public HalfArray4 TyreSlipAngle;
    [FieldOffset(208)] public HalfArray4 SlipRatio;
    [FieldOffset(216)] public HalfArray4 NdSlip;
    [FieldOffset(224)] public HalfArray4 Load;
    [FieldOffset(232)] public Half Steer;
    [FieldOffset(234)] public Half BodyworkVolume;
    [FieldOffset(236)] public Half DrivetrainSpeed;
    
    [FieldOffset(240)] public int LapTime;
    [FieldOffset(244)] public int LastLap;
    [FieldOffset(248)] public int BestLap;
    [FieldOffset(252)] public byte Fuel;
    [FieldOffset(253)] public byte FuelLaps;
    [FieldOffset(254)] public byte Gear;
    [FieldOffset(255)] public ByteArray4 TyreDirtyLevel;
    [FieldOffset(259)] public ByteArray5 DamageZoneLevel;
    [FieldOffset(264)] public byte Gas;
    [FieldOffset(265)] public byte Brake;
    [FieldOffset(266)] public byte LapCount;
    [FieldOffset(267)] public byte Boh;
    [FieldOffset(268)] public ushort Status;

    [FieldOffset(272)] public byte CarDirt;
    [FieldOffset(273)] public byte EngineLife;
    [FieldOffset(274)] public byte TurboBoost;
    [FieldOffset(275)] public bool Connected;
}

[InlineArray(4)]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vector3hArray4
{
    private Vector3h _element;
}

[InlineArray(4)]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vector3Array4
{
    private Vector3 _element;
}

[InlineArray(4)]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ByteArray4
{
    private byte _element;
}

[InlineArray(5)]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ByteArray5
{
    private byte _element;
}
