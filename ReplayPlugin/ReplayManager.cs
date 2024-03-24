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

    public void WriteReplay(List<ReplayFrame> frames, byte sessionId)
    {
        using var file = File.Create("out.acreplay");
        using var writer = new ReplayWriter(file);
        
        writer.Write(16);
        writer.Write(1000.0 / _configuration.Server.RefreshRateHz);
        writer.WriteString("03_clear");
        writer.WriteString(_configuration.CSPTrackOptions.Track);
        writer.WriteString(_configuration.Server.TrackConfig);
        
        writer.Write((uint) _entryCarManager.EntryCars.Length);
        writer.Write(frames.Count);
        
        writer.Write((uint) frames.Count);
        writer.Write(0);
        
        foreach (var frame in frames)
        {
            var trackFrame = new ReplayTrackFrame
            {
                SunAngle = frame.SunAngle
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
        
            writer.Write((uint) frames.Count);
            writer.Write(0);
            
            // frames here
            foreach (var frame in frames)
            {
                var found = false;
                foreach (var carFrame in frame.CarFrames.Span)
                {
                    if (carFrame.SessionId == i)
                    {
                        found = true;
                        carFrame.ToWriter(writer);
                        break;
                    }
                }

                if (frame.AiFrameMapping.TryGetValue(sessionId, out var aiFrameMappingList))
                {
                    foreach (var aiFrameMapping in aiFrameMappingList)
                    {
                        ref var aiFrame = ref frame.AiFrames.Span[aiFrameMapping];
                        if (aiFrame.SessionId == i)
                        {
                            found = true;
                            aiFrame.ToWriter(writer);
                            break;
                        }
                    }
                }

                if (!found)
                {
                    new ReplayCarFrame().ToWriter(writer);
                }
            }
            
            writer.Write(0);
        }
        
        writer.Write(0);
    }
}
