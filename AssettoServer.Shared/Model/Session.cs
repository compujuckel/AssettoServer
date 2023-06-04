namespace AssettoServer.Shared.Model;

public class Session
{
    public int Id { get; set; }
    public SessionType Type { get; set; }
    public virtual string? Name { get; set; }
    public virtual int Time { get; set; }
    public virtual int Laps { get; set; }
}
