namespace AssettoServer.Shared.Model;

public interface IEntryCar
{
    public IClient? Client { get; set; }
    public byte SessionId { get; }
    public string Model { get; }
    public string Skin { get; }
    public bool IsSpectator { get; }
    public float Ballast { get; set; }
    public int Restrictor { get; set; }
    public string? FixedSetup { get; }
    public string LegalTyres { get; }
    public bool ForceLights { get; set; }
    public bool AiControlled { get; }
    public AiMode AiMode { get; set; }
    public string? AiName { get; }

    public bool IsInRange(IEntryCar target, float range);
    public void Reset();
    public void SetActive();
    public void SetAiOverbooking(int count);

    public void SetCollisions(bool enable);

    public bool TryResetPosition();
}

public enum AiMode
{
    None,
    Auto,
    Fixed
}
