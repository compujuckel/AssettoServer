using System.Runtime.CompilerServices;

namespace AssettoServer.Shared.Model;

[InlineArray(Length)]
public struct DamageZoneLevel
{
    private float _element;
    public const int Length = 5;
}
