using AssettoServer.Server;

namespace LogSessionPlugin;

public class EntryCarLogSession
{
    public LogSessionPlayer? PreviousPlayer { get; set; }
    public LogSessionPlayer? CurrentPlayer { get; set; }
    
    public LogSessionLap? CurrentLap { get; set; }
    
    private readonly EntryCar _entryCar;
    private readonly SessionManager _sessionManager;
    
    public EntryCarLogSession(EntryCar entryCar,
        SessionManager sessionManager)
    {
        _entryCar = entryCar;
        _sessionManager = sessionManager;
        _entryCar.ResetInvoked += OnResetInvoked;
    }

    private void OnResetInvoked(EntryCar sender, EventArgs args)
    {
        PreviousPlayer = CurrentPlayer;
        if (PreviousPlayer != null && CurrentPlayer != null)
                PreviousPlayer.SteamId = CurrentPlayer.SteamId;
        
        CurrentPlayer = new LogSessionPlayer(sender)
        {
            StartTime = _sessionManager.ServerTimeMilliseconds
        };
    }

    public void Update()
    {
        var client = _entryCar.Client;
        if (client is not { HasSentFirstUpdate: true } || CurrentPlayer == null)
            return;

        CurrentPlayer.MaxSpeed = Math.Max(CurrentPlayer.MaxSpeed, _entryCar.Status.Velocity.Length() * 3.6);
    }

    public void SetActive()
    {
        if (CurrentPlayer == null) return;
        CurrentPlayer.SteamId = _entryCar.Client!.Guid;
        CurrentPlayer.StartTime = _sessionManager.ServerTimeMilliseconds;
    }

    public void UpdateCollisions(CollisionEventArgs args)
    {
        if (CurrentPlayer == null || CurrentPlayer.SteamId == 0) return;
        
        if (args.TargetCar == null)
            CurrentPlayer.EnvironmentCollisions++;
        else
            CurrentPlayer.PlayerCollisions++;
    }

    public void UpdateLaps(LapCompletedEventArgs args)
    {
        if (CurrentPlayer == null || CurrentPlayer.SteamId == 0) return;
        
        CurrentLap ??= new LogSessionLap();

        CurrentLap.Time = args.Packet.LapTime;

        var lap = args.Packet.Laps?.Where(x => x.SessionId == _entryCar.SessionId).FirstOrDefault();
        
        CurrentLap.Position = lap?.RacePos ?? 0;

        if (lap is { HasCompletedLastLap: 1 })
            CurrentPlayer.FinalRacePosition = lap.RacePos;
        
        CurrentLap.Sectors.Add(CurrentLap.Sectors.Count, args.Packet.LapTime - (uint)CurrentLap.Sectors.Sum(x => x.Value));
        
        CurrentPlayer.Laps.Add(CurrentPlayer.Laps.Count, CurrentLap);
        CurrentLap = new LogSessionLap();
    }

    public void UpdateSector(SectorSplitEventArgs args)
    {
        if (CurrentPlayer == null || CurrentPlayer.SteamId == 0) return;
        
        CurrentLap ??= new LogSessionLap();

        if (CurrentLap.Sectors.ContainsKey(args.Packet.SplitIndex))
        {
            CurrentPlayer.Laps.Add(CurrentPlayer.Laps.Count, CurrentLap);
            CurrentLap = new LogSessionLap();
        }

        CurrentLap.Cuts += args.Packet.Cuts;
        CurrentLap.Sectors.Add(args.Packet.SplitIndex, args.Packet.SplitTime);
    }

    public LogSessionPlayer? FinishData()
    {
        if (PreviousPlayer == null || PreviousPlayer.SteamId == 0) return null;
        
        if (CurrentLap?.Sectors.Count > 0)
            PreviousPlayer.Laps.Add(PreviousPlayer.Laps.Count, CurrentLap);
        
        PreviousPlayer.EndTime = _sessionManager.ServerTimeMilliseconds;

        var data = PreviousPlayer;
        PreviousPlayer = null;
        
        return data;
    }
}
