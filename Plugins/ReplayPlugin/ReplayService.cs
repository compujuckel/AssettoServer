using System.Threading.Channels;
using AssettoServer.Server;
using Microsoft.Extensions.Hosting;
using SerilogTimings;

namespace ReplayPlugin;

public class ReplayService : BackgroundService
{
    private readonly ReplayWriter _replayWriter;
    private readonly ReplaySegmentManager _segmentManager;
    private readonly SessionManager _sessionManager;
    
    private Thread _writerLoopThread = null!;
    private readonly Channel<ReplayWriterJob> _writerJobChannel = Channel.CreateBounded<ReplayWriterJob>(new BoundedChannelOptions(10)
    {
        FullMode = BoundedChannelFullMode.Wait
    });
    
    public ReplayService(ReplayWriter replayWriter, ReplaySegmentManager segmentManager, SessionManager sessionManager)
    {
        _replayWriter = replayWriter;
        _segmentManager = segmentManager;
        _sessionManager = sessionManager;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _writerLoopThread = new Thread(() => WriterLoop(stoppingToken))
        {
            Name = "ReplayWriterLoop",
            Priority = ThreadPriority.BelowNormal
        };
        _writerLoopThread.Start();
        return Task.CompletedTask;
    }
    
    public async Task SaveReplayAsync(int timeSeconds, byte targetSessionId, string filename)
    {
        var startTime = Math.Max(0, _sessionManager.ServerTimeMilliseconds - timeSeconds * 1000);
        var endTime = _sessionManager.ServerTimeMilliseconds;
        
        await SaveReplayAsync(startTime, endTime, targetSessionId, filename);
    }

    public async Task SaveReplayAsync(long startTime, long endTime, byte targetSessionId, string filename)
    {
        _segmentManager.PauseCleanup = true;
        var job = new ReplayWriterJob(filename, startTime, endTime, targetSessionId);
        await _writerJobChannel.Writer.WriteAsync(job);
        await job.TaskCompletionSource.Task;
    }

    private void WriterLoop(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ReplayWriterJob? job = null;
            try
            {
                job = _writerJobChannel.Reader.ReadAsync(stoppingToken).AsTask().Result;
                var duration = (job.EndTime - job.StartTime) / 1000;
                using var _ = Operation.Time("Saving replay {Path} with requested duration {Duration} seconds", job.Path, duration);
                _replayWriter.WriteReplay(job.StartTime, job.EndTime, job.TargetSessionId, job.Path);
                job.TaskCompletionSource.SetResult();
            }
            catch (Exception ex)
            {
                job?.TaskCompletionSource.SetException(ex);
            }
            finally
            {
                _segmentManager.PauseCleanup = _writerJobChannel.Reader.Count > 0;
            }
        }
    }
}
