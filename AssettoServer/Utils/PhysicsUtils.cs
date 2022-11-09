using System;

namespace AssettoServer.Utils;

public static class PhysicsUtils
{
    public const float Gravity = 9.80665f;
        
    public static float CalculateBrakingDistance(float speed, float deceleration = Gravity)
    {
        return MathF.Pow(speed, 2) / (2 * deceleration);
    }
        
    public static float CalculateMaxCorneringSpeedSquared(float radius, float friction = 1)
    {
        return Gravity * friction * radius;
    }
    
    public static float CalculateMaxCorneringSpeed(float radius, float friction = 1)
    {
        return MathF.Sqrt(CalculateMaxCorneringSpeedSquared(radius, friction));
    }
}
