using System.Numerics;
using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Model;

public class CarStatus
{
    public DamageZoneLevel DamageZoneLevel { get; set; }
    public short P2PCount { get; set; }
    public bool MandatoryPit { get; set; }
    public string? CurrentTyreCompound { get; set; }

    public byte PakSequenceId { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public Vector3 Velocity { get; set; }
    public long Timestamp { get; set; }
    public byte[] TyreAngularSpeed { get; } = [100, 100, 100, 100];
    public byte SteerAngle { get; set; } = 127;
    public byte WheelAngle { get; set; } = 127;
    public ushort EngineRpm { get; set; }
    public byte Gear { get; set; } = 1;
    public CarStatusFlags StatusFlag { get; set; }
    public short PerformanceDelta { get; set; }
    public byte Gas { get; set; }
    public float NormalizedPosition { get; set; }

    public float GetRotationAngle()
    {
        float angle = (float)(Rotation.X * 180 / Math.PI);
        if (angle < 0)
            angle += 360;

        return angle;
    }

    public float GetVelocityAngle()
    {
        if (Math.Abs(Velocity.X) < 1 && Math.Abs(Velocity.Z) < 1)
            return GetRotationAngle();

        Vector3 normalizedVelocity = Vector3.Normalize(Velocity);
        float angle = (float)-(Math.Atan2(normalizedVelocity.X, normalizedVelocity.Z) * 180 / Math.PI);
        if (angle < 0)
            angle += 360;

        return angle;
    }
}
