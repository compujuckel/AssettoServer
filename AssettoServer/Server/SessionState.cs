using System.Collections.Generic;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Model;

namespace AssettoServer.Server;

public class SessionState : ISessionState
{
    public ISession Configuration { get; init; }
    public int EndTime { get; set; } // TODO
    public long StartTimeMilliseconds { get; set; }
    public int TimeLeftMilliseconds => (int)(StartTimeMilliseconds + Configuration.Time * 60_000 - _timeSource.ServerTimeMilliseconds);
    public int SessionTimeMilliseconds => (int)(_timeSource.ServerTimeMilliseconds - StartTimeMilliseconds);
    public uint TargetLap { get; set; } = 0;
    public uint LeaderLapCount { get; set; } = 0;
    public bool LeaderHasCompletedLastLap { get; set; } = false;
    public bool SessionOverFlag { get; set; } = false;
    public Dictionary<byte, EntryCarResult>? Results { get; set; }
    public IEnumerable<IEntryCar<IClient>>? Grid { get; set; }

    private readonly SessionManager _timeSource;

    public SessionState(SessionConfiguration configuration, SessionManager timeSource)
    {
        Configuration = configuration;
        _timeSource = timeSource;
    }
}
