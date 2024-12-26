namespace AssettoServer.Server.Configuration.Kunos;
public class EventInfo
{
    public string Type { get; set; } = "Collision";
    public byte CarId { get; set; }
    public object? Driver { get; set; } = new() ;
    public int OtherCarId { get; set; }
    public object? OtherDriver { get; set; } = new();
    public float ImpactSpeed { get; set; }
    public object? WorldPosition { get; set; } = new();
    public object? RelPosition { get; set; } = new();
    public long Time { get; set; }
}