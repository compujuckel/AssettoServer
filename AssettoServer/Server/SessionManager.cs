using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
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
using AssettoServer.Shared.Network.Packets;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Globalization;

namespace AssettoServer.Server;

public class SessionManager : CriticalBackgroundService
{
    private readonly ACServerConfiguration _configuration;
    private readonly Func<SessionConfiguration, SessionState> _sessionStateFactory;
    private readonly Stopwatch _timeSource = new();
    private readonly EntryCarManager _entryCarManager;
    private readonly Lazy<WeatherManager> _weatherManager;
    private readonly string _resultFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "results");

    public int CurrentSessionIndex { get; private set; } = -1;
    public bool IsLastRaceInverted { get; private set; } = false;
    public bool MustInvertGrid { get; private set; } = false;
    public SessionState CurrentSession { get; private set; } = null!;

    public long ServerTimeMilliseconds => _timeSource.ElapsedMilliseconds;

    public bool IsOpen => CurrentSession.Configuration.IsOpen switch
    {
        IsOpenMode.Open => true,
        IsOpenMode.CloseAtStart => !CurrentSession.IsStarted,
        _ => false,
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

        _entryCarManager.ClientConnected += OnClientConnected;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _timeSource.Start();
        NextSession();

        await LoopAsync(stoppingToken);
    }

    private async Task LoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));

        while (await timer.WaitForNextTickAsync(token))
        {
            try
            {
                if (IsSessionOver())
                {
                    NextSession();
                    // continue;
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
                            && ServerTimeMilliseconds > _configuration.Server.ResultScreenTime * 1000L + CurrentSession.OverTimeMilliseconds && _configuration.Server.Loop)
                        {
                            
                            NextSession();
                        }

                        if (CurrentSession.HasSentRaceOverPacket
                            && ServerTimeMilliseconds > _configuration.Server.ResultScreenTime * 1000L + CurrentSession.OverTimeMilliseconds && !_configuration.Server.Loop)
                        {
                            // TODO: Exit
                            Log.Information("Exiting server (In Async Loop)");
                            Environment.Exit(0);
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

        Log.Information("Lap completed: SessionId={SessionId}, Name={ClientName}, Cuts:{NumCuts}, Laptime={LapTime}, Sectors={SectorTime}, TyreCompound={TyreCompound}", client.SessionId, client.Name, lap.Cuts, TimeSpan.FromMilliseconds(lap.LapTime).ToString(@"mm\:ss\.ffff"), lap.Splits?.ToList() ?? new List<uint>(), client.EntryCar.Status.CurrentTyreCompound);
        
        // Find the DriverName from the EntryCar
        var driverName = _entryCarManager.EntryCars.FirstOrDefault(e => e.SessionId == client.SessionId)?.DriverName ?? client.Name;

        // Add lap information
        CurrentSession.Laps.Add(new LapInfo
        {
            DriverName = driverName,
            DriverGuid = client.Guid,
            CarId = client.SessionId,
            CarModel = client.EntryCar.Model,
            CarSkin = client.EntryCar.Skin,
            Timestamp = ServerTimeMilliseconds,
            LapNumber = entryCarResult.NumLaps,
            LapTime = lap.LapTime,
            Sectors = lap.Splits?.ToList() ?? new List<uint>(),
            Cuts = lap.Cuts,
            BallastKG = (int)client.EntryCar.Ballast,
            Tyre = client.EntryCar.Status.CurrentTyreCompound ?? "",
            Restrictor = (int)client.EntryCar.Restrictor
        });

        if (CurrentSession.Configuration.Type == SessionType.Race || lap.Cuts == 0)
        {
            // Update lap information
            entryCarResult.LastLap = lap.LapTime;
            entryCarResult.NumLaps++;
            entryCarResult.TotalTime = (uint)(CurrentSession.SessionTimeMilliseconds - client.EntryCar.Ping / 2);

            // Update best lap if applicable
            if (lap.LapTime < entryCarResult.BestLap)
            {
                entryCarResult.BestLap = lap.LapTime;
            }

            // Update leader lap count
            var oldLeaderLapCount = CurrentSession.LeaderLapCount;
            if (entryCarResult.NumLaps > CurrentSession.LeaderLapCount)
            {
                CurrentSession.LeaderLapCount = entryCarResult.NumLaps;
            }

            // Update race positions based on laps and total time
            if (CurrentSession.Configuration.Type == SessionType.Race)
            {
                foreach (var res in CurrentSession.Results
                                .OrderByDescending(car => car.Value.NumLaps)
                                .ThenBy(car => car.Value.TotalTime)
                                .Select((x, i) => new { Car = x, Index = i }) )
                {
                    res.Car.Value.RacePos = (uint)res.Index;
                }
            }else if(CurrentSession.Configuration.Type == SessionType.Qualifying || CurrentSession.Configuration.Type == SessionType.Practice){
                // Update race positions based on best lap
                foreach (var res in CurrentSession.Results
                                .OrderBy(car => car.Value.BestLap)
                                .Select((x, i) => new { Car = x, Index = i }) )
                {
                    res.Car.Value.RacePos = (uint)res.Index;
                }
            }

            // Check if the session is over
            if (CurrentSession.SessionOverFlag)
            {
                Log.Debug("===RACE OVER===");
                if (CurrentSession.Configuration is { Type: SessionType.Race, IsTimedRace: true })
                {
                    // Handle timed race logic
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
                        Log.Debug("Race Over: EntryCarResult.NumLaps({NumLaps}) <= oldLeaderLapCount({OldLeaderLapCount})",  entryCarResult.NumLaps, oldLeaderLapCount);
                        entryCarResult.HasCompletedLastLap = CurrentSession.LeaderHasCompletedLastLap;
                    }
                    else
                    {
                        Log.Debug("Race Over: LeaderHasCompletedLastLap set to true");
                        Log.Debug("Race Over: EntryCarResult.NumLaps: {NumLaps}, oldLeaderLapCount: {OldLeaderLapCount}",  entryCarResult.NumLaps, oldLeaderLapCount);
                        CurrentSession.LeaderHasCompletedLastLap = true;
                        entryCarResult.HasCompletedLastLap = true;
                    }
                }
                else
                {
                    // For lap-based races
                    entryCarResult.HasCompletedLastLap = true;
                }
            }

            if (CurrentSession.Configuration.Type == SessionType.Race && CurrentSession.Configuration.IsTimedRace && CurrentSession.LeaderHasCompletedLastLap)
            {
                entryCarResult.HasCompletedLastLap = true;
            }

            // Additional session over conditions based on session type
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

    public bool OnClientEvent(ACTcpClient client, ClientEvent.SingleClientEvent evt)
    {
        if (evt.Type == ClientEventType.CollisionWithCar)
        {
            
            var targetCar = _entryCarManager.EntryCars[evt.TargetSessionId];
            CurrentSession.Events.Add(new EventInfo
            {
                Type = "COLLISION_WITH_CAR",
                CarId = client.SessionId,
                Driver = new {
                    Name = client.Name,
                    Guid = client.Guid.ToString(),
                    Team = client.Team,
                    Nation = client.NationCode,
                    GuidsList = new[] { client.Guid.ToString() }
                },
                OtherCarId = targetCar.SessionId,
                OtherDriver = new {
                    Name = targetCar.Client?.Name,
                    Guid = targetCar.Client?.Guid.ToString(),
                    Team = targetCar.Client?.Team,
                    Nation = targetCar.Client?.NationCode,
                    GuidsList = new[] { targetCar.Client?.Guid.ToString() }
                },
                ImpactSpeed = evt.Speed,
                WorldPosition = new {
                    X = evt.Position.X,
                    Y = evt.Position.Y,
                    Z = evt.Position.Z
                },
                RelPosition = new {
                    X = evt.RelPosition.X,
                    Y = evt.RelPosition.Y,
                    Z = evt.RelPosition.Z
                },
                Time = ServerTimeMilliseconds
            });
        }
        else if (evt.Type == ClientEventType.CollisionWithEnv)
        {
            CurrentSession.Events.Add(new EventInfo
            {
                Type = "COLLISION_WITH_ENV",
                CarId = client.SessionId,
                Driver = new {
                    Name = client.Name,
                    Guid = client.Guid.ToString(),
                    Team = client.Team,
                    Nation = client.NationCode,
                    GuidsList = new[] { client.Guid.ToString() }
                },
                OtherCarId = -1,
                OtherDriver = new {
                    Name = "",
                    Team = "",
                    Nation = "",
                    Guid = "",
                    GuidsList = new List<string>()
                },
                ImpactSpeed = evt.Speed,
                WorldPosition = new {
                    X = evt.Position.X,
                    Y = evt.Position.Y,
                    Z = evt.Position.Z
                },
                RelPosition = new {
                    X = evt.RelPosition.X,
                    Y = evt.RelPosition.Y,
                    Z = evt.RelPosition.Z
                },
                Time = ServerTimeMilliseconds
            });
        }
        
        return true;
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

        // if (CurrentSession.Configuration.IsOpen == IsOpenMode.Closed && connectedCount < 2)
        // {
        //     return true;
        // }

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
            if (CurrentSession.OverTimeMilliseconds == 0){
                CurrentSession.OverTimeMilliseconds = overTimeMilliseconds;
                Log.Debug("CurrentSession.OverTimeMilliseconds set to {OverTimeMilliseconds}", overTimeMilliseconds);
            }
                

            if (CurrentSession.OverTimeMilliseconds == overTimeMilliseconds)
            {
                if (_entryCarManager.EntryCars.Where(c => c.Client is { HasSentFirstUpdate: true })
                    .Any(car => CurrentSession.Results?[car.SessionId] is { HasCompletedLastLap: false }))
                {
                    // Log.Debug("There are still cars that have not completed the last lap.");
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
    private void OnClientConnected(ACTcpClient client, EventArgs eventArgs)
    {
        Log.Debug("Client connected: SessionId={SessionId}, Name={Name}, Guid={Guid}, Model={Model}, Skin={Skin}",
            client.SessionId,
            client.Name,
            client.Guid,
            client.EntryCar?.Model,
            client.EntryCar?.Skin);

        // var currentResult = CurrentSession.Results;
        
        if (CurrentSession.Results != null && CurrentSession.Results[client.SessionId].Guid != client.Guid)
        {
            // if LockEntryList is true, then we need to check if the GUID is in the entry list and if it is, then we need to update the name
            if(_configuration.Server.LockedEntryList){
                var entryCar = _entryCarManager.EntryCars[client.SessionId];
                if(entryCar != null && entryCar.Client != null && entryCar.Client.Guid == client.Guid){
                    client.Name = entryCar.DriverName;
                }
            }

            CurrentSession.Results[client.SessionId] = new EntryCarResult(client);
        }    
    }

    public void SetSession(int sessionId)
    {
        var previousSession = CurrentSession;
        Dictionary<byte, EntryCarResult>? previousSessionResults = CurrentSession?.Results; // breaks with CurrentSession.Result don't believe the IDE

        CurrentSession = _sessionStateFactory(_configuration.Sessions[sessionId]);
        CurrentSession.Results = new Dictionary<byte, EntryCarResult>();
        CurrentSession.StartTimeMilliseconds = ServerTimeMilliseconds;

        // Set Sun Angle by time
        if(CurrentSession.Configuration.StartTime != null)
        {
            if (DateTime.TryParseExact(CurrentSession.Configuration.StartTime, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
            {
                _weatherManager.Value.SetTime((int)dateTime.TimeOfDay.TotalSeconds);
            }
            else
            {
                Log.Warning("Invalid time format. Example: 15:31");
            }
        }

        
        if(_configuration.Server.LockedEntryList){

            for (byte entryCarIndex = 0; entryCarIndex < _configuration.EntryList.Cars.Count; entryCarIndex++)
            {
                var car = _configuration.EntryList.Cars[entryCarIndex];
                var entryCar = _entryCarManager.EntryCars[entryCarIndex];
                if (entryCar != null)
                {
                    if (!string.IsNullOrEmpty(car.Guid) && ulong.TryParse(car.Guid, out ulong guidValue))
                    {
                        CurrentSession.Results.Add(entryCar.SessionId, new EntryCarResult(entryCar.Client)
                        {
                            Name = car.DriverName ?? "",
                            Guid = guidValue
                        });
                    }
                    else
                    {
                        Log.Warning("You have locked the entry list, but a car has an invalid or missing GUID. Entry list will be ignored: {CarId}", entryCar.SessionId);
                        // Log.Warning("Car with model {Model} and skin {Skin} has invalid or missing GUID: {Guid}. CarId: {CarId}", car.Model, car.Skin, car.Guid, entryCar.SessionId);
                    }
                }
            }
        }else{
            foreach (var entryCar in _entryCarManager.EntryCars)
            {
                CurrentSession.Results?.Add(entryCar.SessionId, new EntryCarResult(entryCar.Client));
                entryCar.Reset();
            }
        }

        var sessionLength = CurrentSession.Configuration switch
        {
            { Infinite: true } => "Infinite",
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

        _configuration.Server.DynamicTrack.TransferSession();
        // TODO weather

        int invertedCount = 0;
        if (previousSessionResults == null)
        {
            CurrentSession.Grid = _entryCarManager.EntryCars;
        }
        else
        {
            var grid = previousSessionResults
                .OrderBy(result => result.Value.BestLap)
                .ThenBy(result => result.Value.TotalTime)
                .ThenBy(result => result.Key)
                .Select(result => _entryCarManager.EntryCars[result.Key])
                .ToList();

            if (MustInvertGrid)
            {
                var inverted = previousSessionResults
                    .Take(_configuration.Server.InvertedGridPositions)
                    .OrderByDescending(result => result.Value.BestLap)
                    .Select(result => _entryCarManager.EntryCars[result.Key])
                    .ToList();

                for (var i = 0; i < inverted.Count; i++)
                {
                    grid[i] = inverted[i];
                }

                Log.Information("Inverted {Slots} grid slots", inverted.Count);

                invertedCount = inverted.Count;
            }

            CurrentSession.Grid = grid;
        }

        SessionChanged?.Invoke(this, new SessionChangedEventArgs(previousSession, CurrentSession, invertedCount));
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
                // Save results before exiting if they haven't been saved yet
                if (CurrentSession.Results != null && !CurrentSession.HasSentRaceOverPacket)
                {
                    SaveSessionResultsAsync().Wait();
                }
                Log.Information("Exiting server (NextSession)");
                Environment.Exit(0);
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
        // if (CurrentSession.HasSentRaceOverPacket)
        // {
        //     return; // Ensure we only send the race over packet once
        // }

        if (CurrentSession.Results != null){
            // Debug logging for session results
            Log.Debug("Session Over");
            Log.Debug("Results:");
            // CurrentSession.Results = new Dictionary<byte, EntryCarResult>(
            //     CurrentSession.Results.OrderBy(kvp => kvp.Value.RacePos)
            //                           .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            // );

            foreach (var kvp in CurrentSession.Results)
            {
                Log.Debug("Car ID: {CarId}, Driver: {DriverName}, Position: {Position}, Laps: {Laps}, Best Lap: {BestLap}, Total Time: {TotalTime}",
                    kvp.Key,
                    kvp.Value.Name,
                    kvp.Value.RacePos,
                    kvp.Value.NumLaps,
                    TimeSpan.FromMilliseconds(kvp.Value.BestLap).ToString(@"mm\:ss\.fff"),
                    TimeSpan.FromMilliseconds(kvp.Value.TotalTime).ToString(@"hh\:mm\:ss\.fff"));
            }

            

            _entryCarManager.BroadcastPacket(new RaceOver
            {
                IsRace = CurrentSession.Configuration.Type == SessionType.Race,
                PickupMode = true,
                Results = CurrentSession.Results
            });
            SaveSessionResultsAsync().Wait();
        }

        CurrentSession.HasSentRaceOverPacket = true;
        CurrentSession.OverTimeMilliseconds = ServerTimeMilliseconds;
        Log.Information("Race over packet sent at {Time}", DateTime.Now);
    }

    private async Task SaveSessionResultsAsync()
    {
        try
        {
            Log.Information("Saving session results");
            if (CurrentSession.Results == null || CurrentSession.Laps.Count == 0)
            {
                Log.Information("Skipping result save: no laps recorded.");
                return;
            }

            if (!Directory.Exists(_resultFolderPath))
            {
                Directory.CreateDirectory(_resultFolderPath);
            }

            var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{CurrentSession.Configuration.Name?.ToUpper()}.json";
            var filePath = Path.Combine(_resultFolderPath, fileName);

            List<object> cars;
            List<object> result;

            if (_configuration.Server.LockedEntryList)
            {
                cars = _configuration.EntryList.Cars.Select((car, index) =>
                {
                    var entryCar = _entryCarManager.EntryCars[index];
                    return new
                    {
                        CarId = entryCar.SessionId,
                        Driver = new
                        {
                            Name = car.DriverName ?? "",
                            Team = car.Team ?? "",
                            Nation = entryCar.Client?.NationCode ?? "",
                            Guid = car.Guid ?? "",
                            GuidsList = !string.IsNullOrEmpty(car.Guid) ? new[] { car.Guid } : new string[0]
                        },
                        Model = car.Model,
                        Skin = car.Skin ?? "",
                        BallastKG = (int)entryCar.Ballast,
                        Restrictor = entryCar.Restrictor,
                        isSpectator = entryCar.IsSpectator,
                    };
                }).Cast<object>().ToList();

                result = CurrentSession.Results.Values.Select((result, index) =>
                {
                    var car = _configuration.EntryList.Cars.FirstOrDefault(c => c.Guid == result.Guid.ToString());
    
                    return new
                    {
                        DriverName = car?.DriverName,
                        DriverGuid = result.Guid.ToString(),
                        CarId = index,
                        CarModel = car?.Model ?? "",
                        Skin = car?.Skin ?? "",
                        BestLap = result.BestLap,
                        TotalTime = result.TotalTime,
                        // Position = CurrentSession.Configuration.Type == SessionType.Race ? result.RacePos + 1 : (uint)index + 1,
                        Position = result.RacePos + 1,
                        Laps = result.NumLaps,
                        BallastKG = car != null ? (int)car.Ballast : 0,
                        Restrictor = car?.Restrictor ?? 0,
                        isSpectator = car?.SpectatorMode ?? 0
                    };
                }).OrderBy(r => r.Position).Cast<object>().ToList();
            }
            else
            {
                cars = _entryCarManager.EntryCars.Select((car, index) =>
                {
                    return new
                    {
                        CarId = car.SessionId,
                        Driver = new
                        {
                            Name = car.Client?.Name ?? car.LatestClient?.Name ?? "",
                            Team = car.Client?.Team ?? car.LatestClient?.Team ?? "",
                            Nation = car.Client?.NationCode ?? car.LatestClient?.NationCode ?? "",
                            Guid = car.Client?.Guid.ToString() ?? car.LatestClient?.Guid.ToString() ?? "",
                            GuidsList = new[] { car.Client?.Guid.ToString() ?? car.LatestClient?.Guid.ToString() ?? "" }
                        },
                        Model = car?.Model ?? "",
                        Skin = car?.Skin ?? "",
                        BallastKG = car != null ? (int)car.Ballast : 0,
                        Restrictor = car?.Restrictor ?? 0,
                        isSpectator = car?.IsSpectator ?? false
                    };
                }).Cast<object>().ToList();

                result = _entryCarManager.EntryCars.Select((car, index) =>
                {
                    var result = CurrentSession.Results[car.SessionId];
                    return new
                    {
                        DriverName = car.Client?.Name ?? car.LatestClient?.Name ?? "",
                        DriverGuid = car.Client?.Guid.ToString() ?? car.LatestClient?.Guid.ToString() ?? "",
                        CarId = car.SessionId,
                        CarModel = car.Model ?? "",
                        BestLap = result.BestLap,
                        TotalTime = result.TotalTime,
                        // Position = CurrentSession.Configuration.Type == SessionType.Race ? result.RacePos + 1 : (uint)index + 1,
                        Position = result.RacePos + 1,
                        Laps = result.NumLaps,
                        BallastKG = car != null ? (int)car.Ballast : 0,
                        Restrictor = car?.Restrictor ?? 0,
                        isSpectator = car?.IsSpectator ?? false
                    };
                }).OrderBy(r => r.Position).Cast<object>().ToList();
            }

            var lapDetails = CurrentSession.Laps.Select(lap => new
            {
                lap.DriverName,
                DriverGuid = lap.DriverGuid.ToString(),
                lap.CarId,
                lap.CarModel,
                lap.Timestamp,
                lap.LapNumber,
                lap.LapTime,
                Sectors = lap.Sectors ?? new List<uint>(),
                lap.Cuts,
                lap.BallastKG,
                lap.Tyre,
                lap.Restrictor
            }).ToList();
            
            var resultData = new
            {
                TrackName = _configuration.Server.Track,
                TrackConfig = _configuration.Server.TrackConfig,
                Type = CurrentSession.Configuration.Type.ToString().ToUpper(),
                Cars = cars,
                Result = result,
                Laps = lapDetails,
                Events = CurrentSession.Events.ToList() ?? null
            };

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(resultData, jsonOptions));
            Log.Information("Session completed, saving json file: results/{FileName}", fileName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving session results");
        }
    }
}

