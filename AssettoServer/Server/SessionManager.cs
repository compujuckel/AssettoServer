using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Configuration.Kunos;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Model;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AssettoServer.Server;

public class SessionManager : CriticalBackgroundService
{
    private readonly ACServerConfiguration _configuration;
    private readonly Func<SessionConfiguration, SessionState> _sessionStateFactory;
    private readonly Stopwatch _timeSource = new();
    private readonly EntryCarManager _entryCarManager;
    private readonly Lazy<WeatherManager> _weatherManager;

    public int CurrentSessionIndex { get; private set; } = -1;
    public bool IsLastRaceInverted { get; private set; } = false;
    public bool MustInvertGrid { get; private set; } = false;
    public SessionState CurrentSession { get; private set; } = null!;

    public long ServerTimeMilliseconds => _timeSource.ElapsedMilliseconds;
    
    public bool IsOpen => CurrentSession.Configuration.IsOpen switch
    {
        IsOpenMode.Open => true,
        IsOpenMode.CloseAtStart => !CurrentSession.IsStarted,
        _  => false,
    };
    
    /// <summary>
    /// Fires when a new session is started
    /// </summary>
    public event EventHandler<SessionManager, SessionChangedEventArgs>? SessionChanged;
    
    public SessionManager(ACServerConfiguration configuration,
        Func<SessionConfiguration, SessionState> sessionStateFactory,
        EntryCarManager entryCarManager,
        Lazy<WeatherManager> weatherManager,
        IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _configuration = configuration;
        _sessionStateFactory = sessionStateFactory;
        _entryCarManager = entryCarManager;
        _weatherManager = weatherManager;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _timeSource.Start();
        NextSession();
        
        await LoopAsync(stoppingToken);
    }

    private async Task LoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));

        while (await timer.WaitForNextTickAsync(token))
        {
            try
            {
                if (IsSessionOver())
                {
                    NextSession();
                }

                switch (CurrentSession.Configuration.Type)
                {
                    case SessionType.Qualifying or SessionType.Practice:
                    {
                        if (CurrentSession is { SessionOverFlag: true, HasSentRaceOverPacket: false })
                        {
                            CalcOverTime();
                            CurrentSession.EndTimeMilliseconds = 60_000 * CurrentSession.Configuration.Time + CurrentSession.StartTimeMilliseconds;
                            if (ServerTimeMilliseconds - CurrentSession.EndTimeMilliseconds > CurrentSession.OverTimeMilliseconds)
                                SendSessionOver();
                        }
                        
                        if (CurrentSession.HasSentRaceOverPacket
                            && ServerTimeMilliseconds > _configuration.Server.ResultScreenTime * 1000L + CurrentSession.OverTimeMilliseconds)
                        {
                            NextSession();
                        }
                        
                        break;
                    }
                    case SessionType.Race:
                    {
                        if (CurrentSession is { EndTimeMilliseconds: not 0L, HasSentRaceOverPacket: false })
                        {
                            CalcOverTime();
                            if (ServerTimeMilliseconds - CurrentSession.EndTimeMilliseconds > CurrentSession.OverTimeMilliseconds)
                                SendSessionOver();
                        }
                        
                        if (CurrentSession.HasSentRaceOverPacket
                            && ServerTimeMilliseconds > _configuration.Server.ResultScreenTime * 1000L + CurrentSession.OverTimeMilliseconds)
                        {
                            NextSession();
                        }
                            
                        break;
                    }
                }

                SendSessionStart();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in session service update");
            }
        }
    }
    
    public bool OnLapCompleted(ACTcpClient client, LapCompletedIncoming lap)
    {
        int timestamp = (int)ServerTimeMilliseconds;

        var entryCarResult = CurrentSession.Results?[client.SessionId] ?? throw new InvalidOperationException("Current session does not have results set");
        
        if (entryCarResult.HasCompletedLastLap)
        {
            Log.Debug("Lap rejected by {ClientName}, already finished", client.Name);
            return false;
        }

        if (CurrentSession.Configuration.Type == SessionType.Race 
            && entryCarResult.NumLaps >= CurrentSession.Configuration.Laps 
            && !CurrentSession.Configuration.IsTimedRace)
        {
            Log.Debug("Lap rejected by {ClientName}, race over", client.Name);
            return false;
        }

        Log.Information("Lap completed by {ClientName}, {NumCuts} cuts, laptime {LapTime}", client.Name, lap.Cuts, TimeSpan.FromMilliseconds(lap.LapTime).ToString(@"mm\:ss\.ffff"));

        if (CurrentSession.Configuration.Type == SessionType.Race || lap.Cuts == 0)
        {
            entryCarResult.LastLap = lap.LapTime;
            entryCarResult.NumLaps++;
            entryCarResult.TotalTime = (uint)(CurrentSession.SessionTimeMilliseconds - client.EntryCar.Ping / 2);
            
            if (lap.LapTime < entryCarResult.BestLap)
            {
                entryCarResult.BestLap = lap.LapTime;
            }

            var oldLeaderLapCount = CurrentSession.LeaderLapCount;
            if (entryCarResult.NumLaps > CurrentSession.LeaderLapCount)
            {
                CurrentSession.LeaderLapCount = entryCarResult.NumLaps;
            }

            if (CurrentSession.SessionOverFlag)
            {
                if (CurrentSession.Configuration is { Type: SessionType.Race, IsTimedRace: true })
                {
                    if (_configuration.Server.HasExtraLap)
                    {
                        if (entryCarResult.NumLaps <= oldLeaderLapCount)
                        {
                            entryCarResult.HasCompletedLastLap = CurrentSession.LeaderHasCompletedLastLap;
                        }
                        else if (CurrentSession.TargetLap > 0)
                        {
                            if (entryCarResult.NumLaps >= CurrentSession.TargetLap)
                            {
                                CurrentSession.LeaderHasCompletedLastLap = true;
                                entryCarResult.HasCompletedLastLap = true;
                            }
                        }
                        else
                        {
                            CurrentSession.TargetLap = entryCarResult.NumLaps + 1;
                        }
                    }
                    else if (entryCarResult.NumLaps <= oldLeaderLapCount)
                    {
                        entryCarResult.HasCompletedLastLap = CurrentSession.LeaderHasCompletedLastLap;
                    }
                    else
                    {
                        CurrentSession.LeaderHasCompletedLastLap = true;
                        entryCarResult.HasCompletedLastLap = true;
                    }
                }
                else
                {
                    entryCarResult.HasCompletedLastLap = true;
                }
            }

            if (CurrentSession.Configuration.Type != SessionType.Race)
            {
                if (CurrentSession.EndTimeMilliseconds != 0)
                {
                    entryCarResult.HasCompletedLastLap = true;
                }
            }
            else if (CurrentSession.Configuration.IsTimedRace)
            {
                if (CurrentSession is { LeaderHasCompletedLastLap: true, EndTimeMilliseconds: 0 })
                {
                    CurrentSession.EndTimeMilliseconds = timestamp;
                }
            }
            else if (entryCarResult.NumLaps != CurrentSession.Configuration.Laps)
            {
                if (CurrentSession.EndTimeMilliseconds == 0)
                    return true;
                entryCarResult.HasCompletedLastLap = true;
            }
            else switch (entryCarResult.HasCompletedLastLap)
            {
                case false:
                    if (CurrentSession.EndTimeMilliseconds == 0)
                        CurrentSession.EndTimeMilliseconds = timestamp;
                    entryCarResult.HasCompletedLastLap = true;
                    break;
                case true when CurrentSession.EndTimeMilliseconds == 0:
                    return true;
                case true:
                    entryCarResult.HasCompletedLastLap = true;
                    break;
            }

            return true;
        }

        if (CurrentSession.EndTimeMilliseconds == 0)
            return true;

        entryCarResult.HasCompletedLastLap = true;
        return false;
    } 
    
    private bool IsSessionOver()
    {
        if (CurrentSession.Configuration.Infinite)
        {
            return false;
        }
        
        if (CurrentSession.Configuration.Type == SessionType.Booking)
        {
            // TODO Currently unused, maybe for later, when i care about sessions without pickup mode :shrug:
            return CurrentSession.TimeLeftMilliseconds == 0;
        }
        
        if (ServerTimeMilliseconds <= CurrentSession.StartTimeMilliseconds)
        {
            return false;
        }

        var connectedCount = _entryCarManager.EntryCars.Count(e => e.Client != null);
        
        if (CurrentSession.Configuration.Type != SessionType.Race)
        {
            return false;
        }

        if (CurrentSession.Configuration.IsOpen == IsOpenMode.Closed && connectedCount < 2)
        {
            return true;
        }
        
        if (CurrentSession.Configuration.IsOpen != IsOpenMode.CloseAtStart)
        {
            return connectedCount == 0;
        }

        switch (CurrentSession.Configuration.Type)
        {
            case SessionType.Race when connectedCount > 0 && Program.IsDebugBuild:
                return false;
            case SessionType.Race when connectedCount == 0 && Program.IsDebugBuild:
                return true;
            case SessionType.Race when connectedCount <= 1:
                return true;
            default:
                return false;
        }
    }

    private void CalcOverTime()
    {
        if (_entryCarManager.EntryCars.All(c => c.Client == null))
        {
            CurrentSession.OverTimeMilliseconds = 0;
            return;
        }

        if (CurrentSession.Configuration.Type == SessionType.Race)
        {
            var overTimeMilliseconds = _configuration.Server.RaceOverTime * 1000L;
            if (CurrentSession.OverTimeMilliseconds == 0)
                CurrentSession.OverTimeMilliseconds = overTimeMilliseconds;
            
            if (CurrentSession.OverTimeMilliseconds == overTimeMilliseconds)
            {
                if (_entryCarManager.EntryCars.Where(c => c.Client is { HasSentFirstUpdate: true })
                    .Any(car => CurrentSession.Results?[car.SessionId] is { HasCompletedLastLap: false }))
                {
                    return;
                }
            }
        }
        else
        {
            var overTimeMilliseconds = ServerTimeMilliseconds / 100 * _configuration.Server.QualifyMaxWait;
            if (CurrentSession.OverTimeMilliseconds == 0 || CurrentSession.OverTimeMilliseconds > overTimeMilliseconds)
                CurrentSession.OverTimeMilliseconds = overTimeMilliseconds;

            if (_entryCarManager.EntryCars
                .Where(c => c.Client is { HasSentFirstUpdate: true })
                .Any(car => CurrentSession.Results?[car.SessionId] is { HasCompletedLastLap: false } 
                            && car.Status.Velocity.LengthSquared() > 5))
            {
                return;
            }
        }
        
        CurrentSession.OverTimeMilliseconds = 1;
    }

    public void SetSession(int sessionId)
    {
        // TODO reset sun angle
        
        var previousSession = CurrentSession;
        Dictionary<byte, EntryCarResult>? previousSessionResults = CurrentSession?.Results; // breaks with CurrentSession.Result don't believe the IDE

        CurrentSession = _sessionStateFactory(_configuration.Sessions[sessionId]);
        CurrentSession.Results = new Dictionary<byte, EntryCarResult>();
        CurrentSession.StartTimeMilliseconds = ServerTimeMilliseconds;

        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            CurrentSession.Results?.Add(entryCar.SessionId, new EntryCarResult());
            entryCar.Reset();
        }

        var sessionLength = CurrentSession.Configuration switch
        {
            { Infinite: true } => $"Infinite",
            { IsTimedRace: false } => $"{CurrentSession.Configuration.Laps} laps",
            _ => $"{CurrentSession.Configuration.Time} minutes"
        };
        Log.Information("Next session: {SessionName} - Length: {Length}", CurrentSession.Configuration.Name, sessionLength);

        if (CurrentSession.Configuration.Type == SessionType.Race)
        {
            CurrentSession.StartTimeMilliseconds = ServerTimeMilliseconds + (CurrentSession.Configuration.WaitTime * 1000);
        }
        else
        {
            IsLastRaceInverted = false;
        }

        // TODO dynamic track
        // TODO weather

        if (previousSessionResults == null)
        {
            CurrentSession.Grid = _entryCarManager.EntryCars;
        }
        else
        {
            var grid = previousSessionResults
                .OrderBy(result => result.Value.BestLap)
                .Select(result => _entryCarManager.EntryCars[result.Key])
                .ToList();

            if (MustInvertGrid)
            {
                var inverted = previousSessionResults
                    .Take(5)
                    .OrderByDescending(result => result.Value.BestLap)
                    .Select(result => _entryCarManager.EntryCars[result.Key])
                    .ToList();

                for (var i = 0; i < inverted.Count; i++)
                {
                    grid[i] = inverted[i];
                }

                Log.Information("Inverted {Slots} grid slots", inverted.Count);
            }

            CurrentSession.Grid = grid;
        }

        SessionChanged?.Invoke(this, new SessionChangedEventArgs(previousSession, CurrentSession));
        SendCurrentSession();

        Log.Information("Switching session to id {Id}", sessionId);
    }

    public bool RestartSession()
    {
        // StallSessionSwitch
        if (_entryCarManager.EntryCars.Any(c => c.Client is { HasSentFirstUpdate: false }))
            return false;
        

        SetSession(CurrentSessionIndex);
        return true;
    }

    public bool NextSession()
    {
        // StallSessionSwitch
        if (_entryCarManager.EntryCars.Any(c => c.Client is { HasSentFirstUpdate: false }))
            return false;
        
        MustInvertGrid = false;
        if (_configuration.Sessions.Count - 1 == CurrentSessionIndex)
        {
            if (_configuration.Server.Loop)
            {
                Log.Information("Looping sessions");
            }
            else if (CurrentSession.Configuration.Type != SessionType.Race || _configuration.Server.InvertedGridPositions == 0 || IsLastRaceInverted)
            {
                // TODO exit
            }

            if (CurrentSession.Configuration.Type == SessionType.Race && _configuration.Server.InvertedGridPositions != 0)
            {
                if (_configuration.Sessions.Count <= 1)
                {
                    MustInvertGrid = true;
                }
                else if (!IsLastRaceInverted)
                {
                    MustInvertGrid = true;
                    IsLastRaceInverted = true;
                    --CurrentSessionIndex;
                }
            }
        }

        if (++CurrentSessionIndex >= _configuration.Sessions.Count)
        {
            CurrentSessionIndex = 0;
        }
        SetSession(CurrentSessionIndex);
        return true;
    }

    public void SendCurrentSession(ACTcpClient? target = null)
    {
        var packet = new CurrentSessionUpdate
        {
            CurrentSession = CurrentSession.Configuration,
            Grid = CurrentSession.Grid,
            TrackGrip = _weatherManager.Value.CurrentWeather.TrackGrip
        };

        if (target == null)
        {
            foreach (var car in _entryCarManager.EntryCars.Where(c => c.Client is { HasSentFirstUpdate: true }))
            {
                packet.StartTime = CurrentSession.StartTimeMilliseconds - car.TimeOffset;
                car.Client?.SendPacket(packet);
            }
        }
        else
        {
            target.SendPacket(packet);
        }
    }

    private void SendSessionStart()
    {
        if (ServerTimeMilliseconds >= CurrentSession.StartTimeMilliseconds + 5000
            && ServerTimeMilliseconds - CurrentSession.LastRaceStartUpdateMilliseconds <= 1000) return;
        
        foreach (var car in _entryCarManager.EntryCars.Where(c => c.Client is { HasSentFirstUpdate: true }))
        {
            car.Client?.SendPacketUdp(new RaceStart()
            {
                StartTime = (int)(CurrentSession.StartTimeMilliseconds - car.TimeOffset),
                TimeOffset = (uint)(ServerTimeMilliseconds - car.TimeOffset),
                Ping = car.Ping,
            });
        }

        CurrentSession.LastRaceStartUpdateMilliseconds = ServerTimeMilliseconds;
    }
    
    private void SendSessionOver()
    {
        if (CurrentSession.Results != null)
            _entryCarManager.BroadcastPacket(new RaceOver
            {
                IsRace = CurrentSession.Configuration.Type == SessionType.Race,
                PickupMode = true,
                Results = CurrentSession.Results
            });
        
        CurrentSession.HasSentRaceOverPacket = true;
        CurrentSession.OverTimeMilliseconds = ServerTimeMilliseconds;
    }
}
