using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Serilog;

namespace ReplayPlugin.Data;

public struct ReplayCarFrame
{
    public byte SessionId;
    //public Vector3 BodyTranslation;
    //public Vector3 BodyOrientation;
    public Vector3 WorldTranslation;
    public HVector3 WorldOrientation;
    //public Vector3x4 SuspensionTranslation;
    //public HVector3x4 SuspensionOrientation;
    //public Vector3x4 TyreTranslation;
    //public HVector3x4 TyreOrientation;
    public Vector3 Velocity;
    public Half EngineRpm;
    public HalfArray4 WheelAngularSpeed;
    //public HalfArray4 TyreSlipAngle;
    //public HalfArray4 SlipRatio;
    //public HalfArray4 NdSlip;
    //public HalfArray4 Load;
    public Half Steer;
    //public Half BodyworkVolume;
    public Half DrivetrainSpeed;
    //public int LapTime;
    //public int LastLap;
    //public int BestLap;
    //public byte Fuel;
    //public byte FuelLaps;
    public byte Gear;
    //public byte[] TyreDirtyLevel;
    //public byte[] DamageZoneLevel;
    public byte Gas;
    public byte Brake;
    //public byte LapCount;
    //public byte Boh;
    public ushort Status;
    
    
    // Extra
    //public byte CarDirt;
    //public byte EngineLife;
    //public byte TurboBoost;
    public bool Connected;

    //public WingStatus[] Wings;
    
    public void ToWriter(ReplayWriter writer)
    {
        var before = writer.BaseStream.Position;
        //writer.WriteStruct(BodyTranslation);
        //writer.WriteHalfVector3(BodyOrientation);
        writer.WritePadding(18);
        writer.Write((short)0); // PAD
        writer.WriteStruct(WorldTranslation);
        writer.WriteStruct(WorldOrientation);
        writer.Write((short)0); // PAD
        writer.WriteStruct(new Vector3x4()); //SuspensionTranslation);
        writer.WriteStruct(new HVector3x4()); //SuspensionOrientation);
        writer.WriteStruct(new Vector3x4()); //TyreTranslation);
        writer.WriteStruct(new HVector3x4()); //TyreOrientation);
        writer.WriteHalfVector3(Velocity);
        writer.Write(EngineRpm);
        writer.WriteStruct(WheelAngularSpeed);
        //writer.WriteStruct(TyreSlipAngle);
        //writer.WriteStruct(SlipRatio);
        //writer.WriteStruct(NdSlip);
        //writer.WriteStruct(Load);
        writer.WritePadding(32);
        writer.Write(Steer);
        writer.Write((short)0); //BodyworkVolume);
        writer.Write(DrivetrainSpeed);
        writer.Write((short)0); // PAD
        //writer.Write(LapTime);
        //writer.Write(LastLap);
        //writer.Write(BestLap);
        writer.WritePadding(12);
        writer.Write((byte)0); //Fuel);
        //writer.Write(FuelLaps);
        writer.Write((byte)0); // PAD
        writer.Write(Gear);
        writer.Write((int)0);
        writer.WritePadding(5);
        //writer.WriteArrayFixed<byte>(TyreDirtyLevel, 4);
        //writer.WriteArrayFixed<byte>(DamageZoneLevel, 5);
        writer.Write(Gas);
        writer.Write(Brake);
        writer.Write((byte)0); //LapCount);
        writer.Write((byte)0); //Boh);
        writer.Write(Status);
        writer.Write((short)0); // PAD

        var beforeExtra = writer.BaseStream.Position;
        //Log.Debug("Size of Main: {0}", beforeExtra - before);
        
        // Extra / Additional Data
        //writer.Write(CarDirt);
        //writer.Write(EngineLife);
        //writer.Write(TurboBoost);
        writer.Write((byte)0);
        writer.Write((byte)255);
        writer.Write((byte)0);
        writer.Write(Connected);

        var beforeWings = writer.BaseStream.Position;
        //Log.Debug("Size of Extra: {0}", beforeWings - beforeExtra);

        /*ReadOnlySpan<byte> boh = stackalloc byte[3];
        foreach (var wing in Wings)
        {
            writer.Write(wing.Angle);
            writer.Write(boh);
            //writer.WriteArrayFixed<byte>(wing.Boh, 3);
        }*/
    }
}

public struct WingStatus
{
    public byte Angle; 
    public byte[] Boh;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct HVector3
{
    public Half X;
    public Half Y;
    public Half Z;

    public HVector3(Vector3 vec)
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

[InlineArray(4)]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct HVector3x4
{
    private HVector3 _element;
}

[InlineArray(4)]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vector3x4
{
    private Vector3 _element;
}
