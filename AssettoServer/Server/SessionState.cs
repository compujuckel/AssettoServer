using System;
using System.Collections.Generic;
using System.Linq;
using AssettoServer.Server.Configuration.Kunos;
using AssettoServer.Shared.Model;

namespace AssettoServer.Server;

public class SessionState
{
    public SessionConfiguration Configuration { get; }
    public long EndTimeMilliseconds { get; set; }
    public long StartTimeMilliseconds { get; set; }
    public int TimeLeftMilliseconds => Configuration.Infinite ? Configuration.Time * 60_000 : (int)Math.Max(0, StartTimeMilliseconds + Configuration.Time * 60_000 - _timeSource.ServerTimeMilliseconds);
    public long SessionTimeMilliseconds => _timeSource.ServerTimeMilliseconds - StartTimeMilliseconds;
    public uint TargetLap { get; set; } = 0;
    public uint LeaderLapCount { get; set; } = 0;
    public bool LeaderHasCompletedLastLap { get; set; } = false;
    public bool IsCutoffReached => _timeSource.ServerTimeMilliseconds > StartTimeMilliseconds - 20_000;

    public bool SessionOverFlag => Configuration switch
    {
        { Type: SessionType.Practice, Infinite: true } => false,
        { Type: SessionType.Practice or SessionType.Qualifying } => _timeSource.ServerTimeMilliseconds > StartTimeMilliseconds 
                                                                    && SessionTimeMilliseconds > Configuration.Time * 60_000,
        { Type: SessionType.Race, IsTimedRace: true } => SessionTimeMilliseconds > Configuration.Time * 60_000 &&
                                                         EndTimeMilliseconds == 0,
        _ => false
    };

    public long OverTimeMilliseconds { get; set; } = 0;
    public bool HasSentRaceOverPacket { get; set; } = false;
    public long LastRaceStartUpdateMilliseconds { get; set; }
    public Dictionary<byte, EntryCarResult>? Results { get; set; }
    public IEnumerable<IEntryCar>? Grid { get; set; }

    private readonly SessionManager _timeSource;

    public SessionState(SessionConfiguration configuration, SessionManager timeSource)
    {
        Configuration = configuration;
        _timeSource = timeSource;
    }
}
