using System.Runtime.CompilerServices;
using System.Text.Json;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using ReplayPlugin.Data;
using ReplayPlugin.Utils;

namespace ReplayPlugin;

public class ReplayWriter
{
    private readonly ReplayConfiguration _configuration;
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly EntryCarManager _entryCarManager;
    private readonly WeatherManager _weather;
    private readonly EntryCarExtraDataManager _extraData;
    private readonly ReplayMetadataProvider _metadata;
    private readonly ReplaySegmentManager _segmentManager;
    
    private static ReadOnlySpan<byte> CspMagic => "__AC_SHADERS_PATCH_v1__"u8;
    private static ReadOnlySpan<byte> CspExtraStreamMagic => "EXT_EXTRASTREAM_v1"u8;
    private static ReadOnlySpan<byte> CspExtraBlobMagic => "EXT_EXTRABLOB_v1"u8;

    public ReplayWriter(ReplayConfiguration configuration,
        ACServerConfiguration serverConfiguration,
        EntryCarManager entryCarManager,
        WeatherManager weather,
        EntryCarExtraDataManager extraData,
        ReplayMetadataProvider metadata,
        ReplaySegmentManager segmentManager)
    {
        _configuration = configuration;
        _serverConfiguration = serverConfiguration;
        _entryCarManager = entryCarManager;
        _weather = weather;
        _extraData = extraData;
        _metadata = metadata;
        _segmentManager = segmentManager;
    }

    private static uint GetTotalFrameCount(long startTime, long endTime, List<ReplaySegment> segments)
    {
        var totalCount = 0U;
        
        if (segments.Count == 1)
        {
            var segment = segments[0];
            using var accessor = segment.CreateAccessor();
            foreach (var frame in accessor)
            {
                if (frame.Header.ServerTime >= startTime && frame.Header.ServerTime < endTime)
                {
                    totalCount++;
                }
            }
        }
        else if (segments.Count > 1)
        {
            var segment = segments[0];
            using (var accessor = segment.CreateAccessor())
            {
                foreach (var frame in accessor)
                {
                    if (frame.Header.ServerTime >= startTime)
                    {
                        totalCount++;
                    }
                }
            }

            for (int i = 1; i < segments.Count - 1; i++)
            {
                totalCount += (uint)segments[i].Index.Count;
            }
            
            segment = segments[^1];
            using (var accessor = segment.CreateAccessor())
            {
                foreach (var frame in accessor)
                {
                    if (frame.Header.ServerTime < endTime)
                    {
                        totalCount++;
                    }
                }
            }
        }

        return totalCount;
    }

    public void WriteReplay(long startTime, long endTime, byte targetSessionId, string outputPath)
    {
        var segments = _segmentManager.Segments
            .SkipWhile(s => s.EndTime < startTime)
            .TakeWhile(s => s.StartTime < endTime)
            .ToList();

        if (segments.Count == 0)
        {
            throw new InvalidOperationException("Trying to write replay but no replay segments present");
        }
        
        using var file = File.Create(outputPath);
        using var writer = new BinaryWriter(file);

        using var playerInfoIndexStream = new MemoryStream();
        using var playerInfoIndexWriter = new BinaryWriter(playerInfoIndexStream);
        
        var totalCount = GetTotalFrameCount(startTime, endTime, segments);
        
        var header = new KunosReplayHeader
        {
            RecordingIntervalMs = 1000.0 / ((float)_serverConfiguration.Server.RefreshRateHz / _configuration.RefreshRateDivisor),
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
            using var accessor = segment.CreateAccessor();
            
            foreach (var frame in accessor)
            {
                if (frame.Header.ServerTime < startTime) continue;
                if (frame.Header.ServerTime > endTime) break;
                
                file.Seek(trackFramesPosition, SeekOrigin.Begin);
                writer.WriteStruct(new KunosReplayTrackFrame
                {
                    SunAngle = frame.Header.SunAngle
                });
                trackFramesPosition = file.Position;
                
                playerInfoIndexWriter.Write(frame.Header.PlayerInfoIndex);
                
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
        }
            
        file.Seek(oldPosition, SeekOrigin.Begin);
        writer.Write(0);

        var cspDataStartPosition = writer.BaseStream.Position;
        
        writer.WriteLengthPrefixed(CspExtraStreamMagic);
        writer.WriteCspCompressedExtraData(0xB197F00E9828B262, s =>
        {
            playerInfoIndexStream.WriteTo(s);
        });
        
        writer.WriteLengthPrefixed(CspExtraBlobMagic);
        writer.WriteCspCompressedExtraData(0x86CE5FCE612D18DF, s =>
        {
            JsonSerializer.Serialize(s, _metadata.GenerateMetadata());
        });
        
        writer.Write(CspMagic);
        writer.Write((int)cspDataStartPosition);
        writer.Write(1);
    }
}
