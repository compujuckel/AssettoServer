using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using Humanizer;
using JetBrains.Annotations;
using ReplayPlugin.Data;
using ReplayPlugin.Utils;
using Serilog;

namespace ReplayPlugin;

public class ReplayManager
{
    private static readonly string SegmentPath = Path.Join("cache", "replay", Guid.NewGuid().ToString());
    
    private readonly ReplayConfiguration _configuration;
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly EntryCarManager _entryCarManager;
    private readonly WeatherManager _weather;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarExtraDataManager _extraData;
    private readonly ReplayMetadataProvider _metadata;

    private readonly ConcurrentQueue<ReplaySegment> _segments = [];

    private ReplaySegment _currentSegment;
    
    private static ReadOnlySpan<byte> CspMagic => "__AC_SHADERS_PATCH_v1__"u8;
    private static ReadOnlySpan<byte> CspExtraStreamMagic => "EXT_EXTRASTREAM_v1"u8;
    private static ReadOnlySpan<byte> CspExtraBlobMagic => "EXT_EXTRABLOB_v1"u8;
    
    public ReplayManager(EntryCarManager entryCarManager,
        ACServerConfiguration serverConfiguration,
        WeatherManager weather,
        SessionManager sessionManager,
        ReplayConfiguration configuration,
        EntryCarExtraDataManager extraData,
        ReplayMetadataProvider metadata)
    {
        _entryCarManager = entryCarManager;
        _serverConfiguration = serverConfiguration;
        _weather = weather;
        _sessionManager = sessionManager;
        _configuration = configuration;
        _extraData = extraData;
        _metadata = metadata;

        Directory.CreateDirectory(SegmentPath);
        AddSegment();
    }

    private void PrintStatistics()
    {
        var size = _currentSegment.Size.Bytes();
        var duration = TimeSpan.FromMilliseconds(_currentSegment.EndTime - _currentSegment.StartTime);
        Log.Debug("Last replay segment size {Size}, {Duration} - {Rate}", size, 
            duration.Humanize(maxUnit: TimeUnit.Second), size.Per(duration).Humanize());
    }

    private void CleanupSegments()
    {
        var startTime = _sessionManager.ServerTimeMilliseconds - _configuration.ReplayDurationMilliseconds;
        while (_segments.TryPeek(out var segment) && segment.EndTime < startTime)
        {
            Log.Debug("Removing old replay segment");
            _segments.TryDequeue(out _);
            segment.Dispose();
            _metadata.Cleanup(segment.EndPlayerInfoIndex);
        }
    }

    [MemberNotNull(nameof(_currentSegment))]
    private void AddSegment()
    {
        CleanupSegments();
        
        var segmentSize = _configuration.MinSegmentSizeBytes;
        if (_currentSegment != null)
        {
            PrintStatistics();
            
            var duration = _currentSegment.EndTime - _currentSegment.StartTime;
            var size = _currentSegment.Size;
            segmentSize = (int)Math.Round((double)size / duration * _configuration.SegmentTargetMilliseconds * 1.05);
        }

        segmentSize = Math.Clamp(segmentSize, _configuration.MinSegmentSizeBytes, _configuration.MaxSegmentSizeBytes);
        Log.Debug("Target replay segment size: {Size}", segmentSize.Bytes());

        var oldSegment = _currentSegment;
        _currentSegment = new ReplaySegment(Path.Join(SegmentPath, $"{Guid.NewGuid()}.rs1"), segmentSize);
        _segments.Enqueue(_currentSegment);
        
        oldSegment?.Unload();
    }

    public void AddFrame<TState>(int numCarFrames, int numAiFrames, int numAiMappings, uint playerInfoIndex, TState state,
        [RequireStaticDelegate] ReplaySegment.ReplayFrameAction<TState> action)
    {
        if (!_currentSegment.TryAddFrame(numCarFrames, numAiFrames, numAiMappings, playerInfoIndex, state, action))
        {
            AddSegment();
            AddFrame(numCarFrames, numAiFrames, numAiMappings, playerInfoIndex, state, action);
        }
    }

    public void WriteReplay(int timeSeconds, byte targetSessionId, string filename)
    {
        var startTime = Math.Max(0, _sessionManager.ServerTimeMilliseconds - timeSeconds * 1000);
        var endTime = _sessionManager.ServerTimeMilliseconds;
        
        WriteReplay(startTime, endTime, targetSessionId, filename);
    }

    private static uint GetTotalFrameCount(long startTime, long endTime, List<ReplaySegment> segments)
    {
        var totalCount = 0U;
        
        if (segments.Count == 1)
        {
            foreach (var frame in segments[0])
            {
                if (frame.Header.ServerTime >= startTime && frame.Header.ServerTime < endTime)
                {
                    totalCount++;
                }
            }
        }
        else if (segments.Count > 1)
        {
            foreach (var frame in segments[0])
            {
                if (frame.Header.ServerTime >= startTime)
                {
                    totalCount++;
                }
            }
            
            for (int i = 1; i < segments.Count - 1; i++)
            {
                totalCount += (uint)segments[i].Index.Count;
            }
            
            foreach (var frame in segments[^1])
            {
                if (frame.Header.ServerTime < endTime)
                {
                    totalCount++;
                }
            }
        }

        return totalCount;
    }

    public void WriteReplay(long startTime, long endTime, byte targetSessionId, string filename)
    {
        // TODO make sure no segments are deleted while writing replay
        var segments = _segments
            .SkipWhile(s => s.EndTime < startTime)
            .TakeWhile(s => s.StartTime < endTime)
            .ToList();

        if (segments.Count == 0)
        {
            Log.Error("Trying to write replay but no replay segments present");
            return;
        }
        
        using var file = File.Create(filename);
        using var writer = new BinaryWriter(file);

        using var playerInfoIndexStream = new MemoryStream();
        using var playerInfoIndexWriter = new BinaryWriter(playerInfoIndexStream);
        
        var totalCount = GetTotalFrameCount(startTime, endTime, segments);
        
        var header = new KunosReplayHeader
        {
            RecordingIntervalMs = 1000.0 / _configuration.RefreshRateHz,
            Weather = _weather.CurrentWeather.Type.Graphics,
            Track = _serverConfiguration.CSPTrackOptions.Track,
            TrackConfiguration = _serverConfiguration.Server.TrackConfig,
            CarsNumber = (uint)_entryCarManager.EntryCars.Length,
            CurrentRecordingIndex = totalCount,
            RecordedFrames = totalCount
        };
        header.ToWriter(writer);

        var trackFramesPosition = file.Position;
        file.Seek(Unsafe.SizeOf<KunosReplayTrackFrame>() * totalCount, SeekOrigin.Current);
        
        
        var carFramePositions = new List<long>();
        for (int i = 0; i < _entryCarManager.EntryCars.Length; i++)
        {
            var carHeader = new KunosReplayCarHeader
            {
                CarId = _entryCarManager.EntryCars[i].Model,
                DriverName = $"AssettoServer App Missing ({i})",
                CarSkinId = _entryCarManager.EntryCars[i].Skin,
                CarFrames = totalCount
            };
            carHeader.ToWriter(writer);

            carFramePositions.Add(file.Position);
            file.Seek(Unsafe.SizeOf<KunosReplayCarFrame>() * totalCount, SeekOrigin.Current);
            
            writer.Write(0);
        }

        var oldPosition = file.Position;
        foreach (var segment in segments)
        {
            file.Seek(trackFramesPosition, SeekOrigin.Begin);
            foreach (var frame in segment)
            {
                if (frame.Header.ServerTime < startTime) continue;
                if (frame.Header.ServerTime > endTime) break;
                
                writer.WriteStruct(new KunosReplayTrackFrame
                {
                    SunAngle = frame.Header.SunAngle
                });
                
                playerInfoIndexWriter.Write(frame.Header.PlayerInfoIndex);
            }
            trackFramesPosition = file.Position;

            foreach (var frame in segment)
            {
                if (frame.Header.ServerTime < startTime) continue;
                if (frame.Header.ServerTime > endTime) break;
                
                var aiFrameMappings = Span<short>.Empty;
                for (int i = 0; i < _entryCarManager.EntryCars.Length; i++)
                {
                    file.Seek(carFramePositions[i], SeekOrigin.Begin);
                    
                    var foundFrame = false;
                    var foundAiMapping = false;
                    foreach (var carFrame in frame.CarFrames)
                    {
                        if (carFrame.SessionId == i)
                        {
                            foundFrame = true;
                            carFrame.ToWriter(writer, true, _extraData.Data[carFrame.SessionId]);
                        }

                        if (carFrame.SessionId == targetSessionId)
                        {
                            foundAiMapping = true;
                            aiFrameMappings = frame.GetAiFrameMappings(carFrame.AiMappingStartIndex);
                        }

                        if (foundFrame && foundAiMapping) break;
                    }

                    foreach (var aiFrameMapping in aiFrameMappings)
                    {
                        ref var aiFrame = ref frame.AiFrames[aiFrameMapping];
                        if (aiFrame.SessionId == i)
                        {
                            foundFrame = true;
                            aiFrame.ToWriter(writer, true, _extraData.Data[aiFrame.SessionId]);
                            break;
                        }
                    }

                    if (!foundFrame)
                    {
                        new ReplayCarFrame().ToWriter(writer, false, EntryCarExtraData.Empty);
                    }

                    carFramePositions[i] = file.Position;
                }
            }

            if (segment != _currentSegment)
            {
                segment.Unload();
            }
        }
            
        file.Seek(oldPosition, SeekOrigin.Begin);
        writer.Write(0);

        var cspDataStartPosition = writer.BaseStream.Position;
        
        writer.WriteLengthPrefixed(CspExtraStreamMagic);
        writer.WriteCspCompressedExtraData(0xB197F00E9828B262, w =>
        {
            playerInfoIndexStream.WriteTo(w.BaseStream);
        });
        
        writer.WriteLengthPrefixed(CspExtraBlobMagic);
        writer.WriteCspCompressedExtraData(0x86CE5FCE612D18DF, w =>
        {
            JsonSerializer.Serialize(w.BaseStream, _metadata.GenerateMetadata());
        });
        
        writer.Write(CspMagic);
        writer.Write((int)cspDataStartPosition);
        writer.Write(1);
    }
}
