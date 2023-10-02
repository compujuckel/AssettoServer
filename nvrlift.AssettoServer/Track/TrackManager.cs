using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace nvrlift.AssettoServer.Track;

public class TrackManager : CriticalBackgroundService
{
    private readonly ACServerConfiguration _configuration;
    private readonly SessionManager _timeSource;
    private readonly TrackImplementation _trackImplementation;

    public TrackManager(TrackImplementation trackImplementation,
        ACServerConfiguration configuration, 
        SessionManager timeSource, 
        IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _trackImplementation = trackImplementation;
        _configuration = configuration;
        _timeSource = timeSource;
    }
    public TrackData CurrentTrack { get; private set; } = null!;
    
    public void SetTrack(TrackData track)
    {
        CurrentTrack = track;
        
        // Seems unnecessary to call this here, the async service picks it up anyway.
        // _trackImplementation.ChangeTrack(CurrentTrack);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (CurrentTrack.UpcomingType == null || CurrentTrack.Type == CurrentTrack.UpcomingType)
                {
                    await Task.Delay(10000, stoppingToken);
                }
                else
                {
                    _trackImplementation.ChangeTrack(CurrentTrack);

                    CurrentTrack.Type = CurrentTrack.UpcomingType;
                    CurrentTrack.UpcomingType = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in track service update");
            }
            finally
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
