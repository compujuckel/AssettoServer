namespace AssettoServer.Shared.Model;

public interface IEntry
{
        public string Model { get; }
        public string? Skin { get; }
        public int SpectatorMode { get; }
        public float Ballast { get; }
        public int Restrictor { get; }
        public string? DriverName { get; }
        public string? Team { get; }   
        public string? FixedSetup { get; }
        public string Guid { get; }
        public AiMode AiMode { get; }
        public string ClientType { get; }
        public string? LegalTyres { get; }
}

public enum AiMode
{
    None,
    Auto,
    Fixed
}
