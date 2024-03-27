using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using ReplayPlugin.Data;
using ReplayPlugin.Utils;

namespace ReplayPlugin;

public class ReplayManager
{
    private readonly ACServerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly WeatherManager _weather;

    public ReplayManager(EntryCarManager entryCarManager, ACServerConfiguration configuration, WeatherManager weather)
    {
        _entryCarManager = entryCarManager;
        _configuration = configuration;
        _weather = weather;
    }

    public void WriteReplay(ReplaySegment segment, byte targetSessionId, string filename)
    {
        using var file = File.Create(filename);
        using var writer = new BinaryWriter(file);
        
        writer.Write(16);
        writer.Write(1000.0 / _configuration.Server.RefreshRateHz);
        writer.WriteACString(_weather.CurrentWeather.Type.Graphics);
        writer.WriteACString(_configuration.CSPTrackOptions.Track);
        writer.WriteACString(_configuration.Server.TrackConfig);
        
        writer.Write((uint) _entryCarManager.EntryCars.Length);
        writer.Write(segment.Index.Count);
        
        writer.Write((uint) segment.Index.Count);
        writer.Write(0);
        
        foreach (var frame in segment)
        {
            writer.WriteStruct(new KunosReplayTrackFrame
            {
                SunAngle = frame.Header.SunAngle
            });
        }
        
        for (int i = 0; i < _entryCarManager.EntryCars.Length; i++)
        {
            writer.WriteACString(_entryCarManager.EntryCars[i].Model);
            writer.WriteACString($"Entry {i}");
            writer.WriteACString("");
            writer.WriteACString("");
            writer.WriteACString(_entryCarManager.EntryCars[i].Skin);
        
            writer.Write((uint) segment.Index.Count);
            writer.Write(0);
            
            var aiFrameMappings = Span<short>.Empty;
            
            // frames here
            foreach (var frame in segment)
            {
                var foundFrame = false;
                var foundAiMapping = false;
                foreach (var carFrame in frame.CarFrames)
                {
                    if (carFrame.SessionId == i)
                    {
                        foundFrame = true;
                        carFrame.ToWriter(writer, true);
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
                        aiFrame.ToWriter(writer, true);
                        break;
                    }
                }

                if (!foundFrame)
                {
                    new ReplayCarFrame().ToWriter(writer, false);
                }
            }
            
            writer.Write(0);
        }
        
        writer.Write(0);
    }
}
