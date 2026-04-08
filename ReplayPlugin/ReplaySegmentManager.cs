using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using AssettoServer.Server;
using Humanizer;
using JetBrains.Annotations;
using ReplayPlugin.Data;
using Serilog;

namespace ReplayPlugin;

public class ReplaySegmentManager
{
    private static readonly string SegmentPath = Path.Join("cache", "replay", Guid.NewGuid().ToString());
    
    private readonly ReplayConfiguration _configuration;
    private readonly SessionManager _sessionManager;
    private readonly ReplayMetadataProvider _metadata;
    
    public ConcurrentQueue<ReplaySegment> Segments { get; } = [];
    public bool PauseCleanup { get; set; }
    
    public ReplaySegment CurrentSegment { get; private set; }
    private ReplaySegment.ReplaySegmentAccessor _currentSegmentAccessor;
    
    public ReplaySegmentManager(SessionManager sessionManager,
        ReplayConfiguration configuration,
        ReplayMetadataProvider metadata)
    {
        _sessionManager = sessionManager;
        _configuration = configuration;
        _metadata = metadata;

        Directory.CreateDirectory(SegmentPath);
        AddSegment();
    }

    private void PrintStatistics()
    {
        var size = CurrentSegment.Size.Bytes();
        var duration = TimeSpan.FromMilliseconds(CurrentSegment.EndTime - CurrentSegment.StartTime);
        Log.Debug("Last replay segment size {Size}, {Duration} - {Rate}", size, 
            duration.Humanize(maxUnit: TimeUnit.Second), size.Per(duration).Humanize());
    }

    private void CleanupSegments()
    {
        if (PauseCleanup) return;
        
        var startTime = _sessionManager.ServerTimeMilliseconds - _configuration.ReplayDurationMilliseconds;
        while (Segments.TryPeek(out var segment) && segment.EndTime < startTime)
        {
            if (segment.IsBusy) continue;
            
            Log.Debug("Removing old replay segment");
            Segments.TryDequeue(out _);
            segment.Dispose();
            _metadata.Cleanup(segment.EndPlayerInfoIndex);
        }
    }

    [MemberNotNull(nameof(CurrentSegment), nameof(_currentSegmentAccessor))]
    private void AddSegment()
    {
        CleanupSegments();
        
        var segmentSize = _configuration.MinSegmentSizeBytes;
        if (CurrentSegment != null)
        {
            PrintStatistics();
            
            var duration = CurrentSegment.EndTime - CurrentSegment.StartTime;
            var size = CurrentSegment.Size;
            segmentSize = (int)Math.Round((double)size / duration * _configuration.SegmentTargetMilliseconds * 1.05);
        }

        segmentSize = Math.Clamp(segmentSize, _configuration.MinSegmentSizeBytes, _configuration.MaxSegmentSizeBytes);
        Log.Debug("Target replay segment size: {Size}", segmentSize.Bytes());
        
        var newSegment = new ReplaySegment(Path.Join(SegmentPath, $"{Guid.NewGuid()}.rs1"), segmentSize);
        var newSegmentLock = newSegment.CreateAccessor();
        CurrentSegment = newSegment;
        _currentSegmentAccessor?.Dispose();
        _currentSegmentAccessor = newSegmentLock;
        Segments.Enqueue(CurrentSegment);
    }

    public void AddFrame<TState>(int numCarFrames, int numAiFrames, int numAiMappings, uint playerInfoIndex, TState state,
        [RequireStaticDelegate, InstantHandle] ReplaySegment.ReplayFrameAction<TState> action)
    {
        if (!_currentSegmentAccessor.TryAddFrame(numCarFrames, numAiFrames, numAiMappings, playerInfoIndex, state, action))
        {
            AddSegment();
            AddFrame(numCarFrames, numAiFrames, numAiMappings, playerInfoIndex, state, action);
        }
    }
}
