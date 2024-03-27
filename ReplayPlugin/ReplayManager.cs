using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using ReplayPlugin.Data;

namespace ReplayPlugin;

public class ReplayManager
{
    private readonly ACServerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;

    public ReplayManager(EntryCarManager entryCarManager, ACServerConfiguration configuration)
    {
        _entryCarManager = entryCarManager;
        _configuration = configuration;
    }

    public void WriteReplay(ReplaySegment segment, byte targetSessionId, string filename)
    {
        using var file = File.Create(filename);
        using var writer = new ReplayWriter(file);
        
        writer.Write(16);
        writer.Write(1000.0 / _configuration.Server.RefreshRateHz);
        writer.WriteString("03_clear");
        writer.WriteString(_configuration.CSPTrackOptions.Track);
        writer.WriteString(_configuration.Server.TrackConfig);
        
        writer.Write((uint) _entryCarManager.EntryCars.Length);
        writer.Write(segment.Index.Count);
        
        writer.Write((uint) segment.Index.Count);
        writer.Write(0);
        
        foreach (ReplayFrame frame in segment)
        {
            var trackFrame = new ReplayTrackFrame
            {
                SunAngle = frame.Header.SunAngle
            };
            trackFrame.ToWriter(writer);
        }
        
        for (int i = 0; i < _entryCarManager.EntryCars.Length; i++)
        {
            writer.WriteString(_entryCarManager.EntryCars[i].Model);
            writer.WriteString($"Entry {i}");
            writer.WriteString("");
            writer.WriteString("");
            writer.WriteString(_entryCarManager.EntryCars[i].Skin);
        
            writer.Write((uint) segment.Index.Count);
            writer.Write(0);
            
            Span<short> aiFrameMappings = Span<short>.Empty;
            
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
