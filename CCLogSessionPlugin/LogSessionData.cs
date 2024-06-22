using AssettoServer.Server;

namespace LogSessionPlugin;

public class LogSessionData
{
    public int ServerId { get; set; }
    public required string Track { get; set; }
    public required string TrackConfig { get; set; }
    public int SessionType { get; set; }
    public int ReverseGrid { get; set; }
    public required string Reason { get; set; }
    public List<LogSessionPlayer> Players { get; set; } = [];
}

public class LogSessionPlayer
{
    public int SessionId { get; set; }
    public ulong SteamId { get; set; }
    public string Model { get; set; }
    public int FinalRacePosition { get; set; } = -1;
    public bool Dnf => FinalRacePosition == -1;
    public int Distance { get; set; } = 0;
    public double MaxSpeed { get; set; } = 0;
    public long StartTime { get; set; } = 0;
    public long EndTime { get; set; } = 0;
    public int PlayerCollisions { get; set; } = 0;
    public int EnvironmentCollisions { get; set; } = 0;
    public Dictionary<int, LogSessionLap> Laps { get; set; } = [];
    
    public LogSessionPlayer(EntryCar sender)
    {
        Model = sender.Model;
        SessionId = sender.SessionId;
        SteamId = sender.Client?.Guid ?? 0;
    }
}

public class LogSessionLap
{
    public uint Time { get; set; } = 0;
    public  Dictionary<int, uint> Sectors { get; set; } = [];
    public int Cuts { get; set; } = 0;
    public int Position { get; set; } = 0;
}

