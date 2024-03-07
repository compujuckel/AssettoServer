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
        1 => true,
        2 => !CurrentSession.IsStarted,
        _ => false
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
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(token))
        {
            try
            {
                if (CurrentSession.SessionOver ||
                    (CurrentSession.HasSentRaceOverPacket 
                     && ServerTimeMilliseconds > _configuration.Server.ResultScreenTime * 1000 + CurrentSession.OverTimeMilliseconds))
                {
                    NextSession();
                }

            switch (CurrentSession.Configuration.Type)
            {
                case SessionType.Qualifying:
                case SessionType.Practice:
                {
                    if (!IsOverTime() 
                        && CurrentSession.SessionOver
                        && ServerTimeMilliseconds - CurrentSession.EndTimeMilliseconds > CurrentSession.OverTimeMilliseconds)
                    {
                        if (CurrentSession.Results != null)
                            _entryCarManager.BroadcastPacket(new RaceOver
                            {
                                IsRace = false,
                                PickupMode = true,
                                Results = CurrentSession.Results
                            });
                        CurrentSession.HasSentRaceOverPacket = true;
                        CurrentSession.OverTimeMilliseconds = ServerTimeMilliseconds;
                    }

                    break;
                }
                case SessionType.Race:
                {
                    if (!IsRaceOverTime() 
                        && CurrentSession.SessionOver
                        && ServerTimeMilliseconds - CurrentSession.EndTimeMilliseconds > CurrentSession.OverTimeMilliseconds)
                    {
                        if (CurrentSession.Results != null)
                            _entryCarManager.BroadcastPacket(new RaceOver
                            {
                                IsRace = true,
                                PickupMode = true,
                                Results = CurrentSession.Results
                            });
                        CurrentSession.HasSentRaceOverPacket = true;
                        CurrentSession.OverTimeMilliseconds = ServerTimeMilliseconds;
                    }
                        
                    break;
                }
            }

                SendStartSession();
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

        if (CurrentSession.Configuration.Type == SessionType.Race && entryCarResult.NumLaps >= CurrentSession.Configuration.Laps && !CurrentSession.Configuration.IsTimedRace)
        {
            Log.Debug("Lap rejected by {ClientName}, race over", client.Name);
            return false;
        }

        Log.Information("Lap completed by {ClientName}, {NumCuts} cuts, laptime {LapTime}", client.Name, lap.Cuts, lap.LapTime);

        // TODO unfuck all of this

        if (CurrentSession.Configuration.Type == SessionType.Race || lap.Cuts == 0)
        {
            entryCarResult.LastLap = lap.LapTime;
            if (lap.LapTime < entryCarResult.BestLap)
            {
                entryCarResult.BestLap = lap.LapTime;
            }

            entryCarResult.NumLaps++;
            if (entryCarResult.NumLaps > CurrentSession.LeaderLapCount)
            {
                CurrentSession.LeaderLapCount = entryCarResult.NumLaps;
            }

            entryCarResult.TotalTime = (uint)(CurrentSession.SessionTimeMilliseconds - client.EntryCar.Ping / 2);

            if (CurrentSession.SessionOver)
            {
                if (CurrentSession.Configuration.Type is SessionType.Practice or SessionType.Qualifying && !CurrentSession.HasSentRaceOverPacket)
                {
                    CurrentSession.EndTimeMilliseconds = 60_000 * CurrentSession.Configuration.Time + CurrentSession.StartTimeMilliseconds;
                }
                
                if (CurrentSession.Configuration is { Type: SessionType.Race, IsTimedRace: true })
                {
                    if (_configuration.Server.HasExtraLap)
                    {
                        if (entryCarResult.NumLaps <= CurrentSession.LeaderLapCount)
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
                    else if (entryCarResult.NumLaps <= CurrentSession.LeaderLapCount)
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
                if (CurrentSession.EndTimeMilliseconds != 0)
                {
                    entryCarResult.HasCompletedLastLap = true;
                }
            }
            else if (!entryCarResult.HasCompletedLastLap)
            {
                entryCarResult.HasCompletedLastLap = true;
                if (CurrentSession.EndTimeMilliseconds == 0)
                {
                    CurrentSession.EndTimeMilliseconds = timestamp;
                }
            }
            else if (CurrentSession.EndTimeMilliseconds != 0)
            {
                entryCarResult.HasCompletedLastLap = true;
            }

            return true;
        }

        if (CurrentSession.EndTimeMilliseconds == 0)
            return true;

        entryCarResult.HasCompletedLastLap = true;
        return false;
    }

    private bool IsOverTime()
    {
        if (_entryCarManager.EntryCars.All(c => c.Client == null)
            || !CurrentSession.SessionOver 
            || CurrentSession.HasSentRaceOverPacket)
        {
            CurrentSession.OverTimeMilliseconds = 0;
            return false;
        }
        
        var overTimeMilliseconds = (long)(ServerTimeMilliseconds * _configuration.Server.QualifyMaxWait);
        if (CurrentSession.OverTimeMilliseconds == 0 || CurrentSession.OverTimeMilliseconds > overTimeMilliseconds)
            CurrentSession.OverTimeMilliseconds = overTimeMilliseconds;
        
        CurrentSession.OverTimeMilliseconds = overTimeMilliseconds;

        if (_entryCarManager.EntryCars.Any(c => c.Client is { HasSentFirstUpdate: true } && c.Status.Velocity.Length() > 5))
        {
            return true;
        }

        CurrentSession.OverTimeMilliseconds = 1;

        return false;
    }

    private bool IsRaceOverTime()
    {
        if (_entryCarManager.EntryCars.All(c => c.Client == null)
            || !CurrentSession.SessionOver 
            || CurrentSession.HasSentRaceOverPacket)
        {
            CurrentSession.OverTimeMilliseconds = 0;
            return false;
        }
        
        var overTimeMilliseconds = _configuration.Server.RaceOverTime * 1000;
        if (CurrentSession.OverTimeMilliseconds == 0)
            CurrentSession.OverTimeMilliseconds = overTimeMilliseconds;
        else if (CurrentSession.OverTimeMilliseconds == overTimeMilliseconds)
        {
            CurrentSession.OverTimeMilliseconds = overTimeMilliseconds;

            if (_entryCarManager.EntryCars.Where(c => c.Client is { HasSentFirstUpdate: true })
                .Any(car => CurrentSession.Results?[car.SessionId] is { HasCompletedLastLap: false }))
            {
                return true;
            }
        }

        CurrentSession.OverTimeMilliseconds = 1;

        return false;
    }

    public void SetSession(int sessionId)
    {
        // StallSessionSwitch
        if (_entryCarManager.EntryCars.Any(c => c.Client is { HasSentFirstUpdate: false }))
        {
            Log.Information("Stalled session because of connecting client");
            return;
        }
        // TODO reset sun angle

        var previousSession = CurrentSession;
        Dictionary<byte, EntryCarResult>? previousSessionResults = CurrentSession?.Results;

        CurrentSession = _sessionStateFactory(_configuration.Sessions[sessionId]);
        CurrentSession.Results = new Dictionary<byte, EntryCarResult>();
        CurrentSession.StartTimeMilliseconds = ServerTimeMilliseconds;

        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            CurrentSession.Results.Add(entryCar.SessionId, new EntryCarResult());
            entryCar.Reset();
        }

        Log.Information("Next session: {SessionName}", CurrentSession.Configuration.Name);

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
            CurrentSession.Grid = previousSessionResults
                .OrderBy(result => result.Value.BestLap)
                .Select(result => _entryCarManager.EntryCars[result.Key]);
        }

        SessionChanged?.Invoke(this, new SessionChangedEventArgs(previousSession, CurrentSession));
        SendCurrentSession();
        SendStartSession();

        Log.Information("Switching session to id {Id}", sessionId);
    }

    public void RestartSession()
    {
        SetSession(CurrentSessionIndex);
    }

    public void NextSession()
    {
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

    private void SendStartSession()
    {
        if (ServerTimeMilliseconds >= CurrentSession.StartTimeMilliseconds + 5000
            && ServerTimeMilliseconds - CurrentSession.LastRaceStartUpdateMilliseconds <= 1000) return;
        
        foreach (var car in _entryCarManager.EntryCars.Where(c => c.Client is { HasSentFirstUpdate: true }))
        {
            car.Client?.SendPacketUdp(new RaceStart()
            {
                StartTime = (uint)(CurrentSession.StartTimeMilliseconds - car.TimeOffset),
                TimeOffset = (uint)(ServerTimeMilliseconds - car.TimeOffset),
                Ping = car.Ping,
            });
        }

        CurrentSession.LastRaceStartUpdateMilliseconds = ServerTimeMilliseconds;
    }
}
