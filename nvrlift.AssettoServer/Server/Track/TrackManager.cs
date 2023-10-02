using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;

namespace nvrlift.AssettoServer.Server.Track;

public class TrackManager : CriticalBackgroundService
{
    private readonly ACServerConfiguration _configuration;
    private readonly SessionManager _timeSource;

    public TrackManager(ACServerConfiguration configuration, 
        SessionManager timeSource, 
        IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _configuration = configuration;
        _timeSource = timeSource;
    }
}
