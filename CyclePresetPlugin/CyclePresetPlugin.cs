using System.Reflection;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Network.Packets.Shared;
using AssettoServer.Shared.Services;
using AssettoServer.Server.Preset;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace CyclePresetPlugin;

public class CyclePresetPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly EntryCarManager _entryCarManager;
    private readonly PresetManager _presetManager;
    private readonly CyclePresetConfiguration _configuration;
    
    private readonly List<PresetType> _votePresets;
    private readonly List<ACTcpClient> _alreadyVoted = new();
    private readonly List<PresetChoice> _availablePresets = new();
    private bool _votingOpen = false;
    
    private readonly List<PresetType> _adminPresets;
    private PresetData? _adminTrack = null;
    private bool _adminTrackChange = false;
    
    private bool _manualTrackChange = false;
    private bool _voteStarted = false;
    private int _extendVotingSeconds = 0;
    private short _finishVote = 0;

    private class PresetChoice
    {
        public PresetType? Track { get; init; }
        public int Votes { get; set; }
    }

    public CyclePresetPlugin(CyclePresetConfiguration configuration,
        PresetConfigurationManager presetConfigurationManager, 
        ACServerConfiguration acServerConfiguration,
        EntryCarManager entryCarManager,
        PresetManager presetManager,
        IHostApplicationLifetime applicationLifetime,
        CSPServerScriptProvider scriptProvider,
        CSPFeatureManager cspFeatureManager) : base(applicationLifetime)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _presetManager = presetManager;

        _votePresets = presetConfigurationManager.VotingPresetTypes;
        _adminPresets = presetConfigurationManager.AllPresetTypes;
        
        _presetManager.SetTrack(new PresetData(presetConfigurationManager.CurrentConfiguration.ToPresetType(), null)
        {
            IsInit = true,
            TransitionDuration = 0
        });
        
        // Include Client Reconnection Script
        if (acServerConfiguration.Extra.EnableClientMessages)
        {
            using var streamReader = new StreamReader(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("CyclePresetPlugin.lua.reconnectclient.lua")!);
            var reconnectScript = streamReader.ReadToEnd();
            scriptProvider.AddScript(reconnectScript, "reconnectclient.lua");
        }
        
        cspFeatureManager.Add(new CSPFeature { Name = "FREQUENT_TRACK_CHANGES" });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<Task> tasks = new()
        {
            Task.Run(() => ExecuteAdminAsync(stoppingToken), stoppingToken),
            Task.Run(() => ExecuteManualVotingAsync(stoppingToken), stoppingToken),
        };

        if (_configuration.VoteEnabled)
            tasks.Add(Task.Run(() => ExecuteVotingAsync(stoppingToken), stoppingToken));

        await Task.WhenAll(tasks).WaitAsync(stoppingToken);
    }

    internal void ListAllPresets(ACTcpClient client)
    {
        client.SendPacket(new ChatMessage { SessionId = 255, Message = "List of all presets:" });
        for (int i = 0; i < _adminPresets.Count; i++)
        {
            var track = _adminPresets[i];
            client.SendPacket(new ChatMessage { SessionId = 255, Message = $" /presetuse {i} - {track.Name}" });
        }
    }

    internal void GetTrack(ACTcpClient client)
    {
        Log.Information(
            $"Current preset: {_presetManager.CurrentPreset.Type!.Name} - {_presetManager.CurrentPreset.Type!.PresetFolder}");
        client.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message =
                $"Current preset: {_presetManager.CurrentPreset.Type!.Name} - {_presetManager.CurrentPreset.Type!.PresetFolder}"
        });
    }

    internal void SetPreset(ACTcpClient client, int choice)
    {
        var last = _presetManager.CurrentPreset;

        if (choice < 0 && choice >= _adminPresets.Count)
        {
            Log.Information($"Invalid preset choice.");
            client.SendPacket(new ChatMessage { SessionId = 255, Message = "Invalid preset choice." });

            return;
        }

        var next = _adminPresets[choice];

        if (last.Type!.Equals(next))
        {
            Log.Information($"No change made, admin tried setting the current preset.");
            client.SendPacket(new ChatMessage
                { SessionId = 255, Message = $"No change made, you tried setting the current preset." });
        }
        else
        {
            _adminTrack = new PresetData(_presetManager.CurrentPreset.Type, next)
            {
                TransitionDuration = _configuration.TransitionDurationSeconds,
            };
            _adminTrackChange = true;
        }
    }
    
    internal void RandomTrack(ACTcpClient client)
    {
        var last = _presetManager.CurrentPreset;

        PresetType next;
        do
        {
            next = _adminPresets[Random.Shared.Next(_adminPresets.Count)];
        } while (last.Type!.Equals(next));

        _adminTrack = new PresetData(_presetManager.CurrentPreset.Type, next)
        {
            TransitionDuration = _configuration.TransitionDurationSeconds,
        };
        _adminTrackChange = true;
    }

    internal void CountVote(ACTcpClient client, int choice)
    {
        if (!_votingOpen)
        {
            client.SendPacket(new ChatMessage { SessionId = 255, Message = "There is no ongoing track vote." });
            return;
        }

        if (choice >= _availablePresets.Count || choice < 0)
        {
            client.SendPacket(new ChatMessage { SessionId = 255, Message = "Invalid choice." });
            return;
        }

        if (_alreadyVoted.Contains(client))
        {
            client.SendPacket(new ChatMessage { SessionId = 255, Message = "You voted already." });
            return;
        }

        _alreadyVoted.Add(client);

        var votedTrack = _availablePresets[choice];
        votedTrack.Votes++;

        client.SendPacket(new ChatMessage
            { SessionId = 255, Message = $"Your vote for {votedTrack.Track!.Name} has been counted." });
    }

    internal void StartVote(ACTcpClient client)
    {

        if (_voteStarted)
        {
            client.SendPacket(new ChatMessage { SessionId = 255, Message = "Vote already ongoing." });
            return;
        }
        _manualTrackChange = true;
    }
    
    internal void FinishVote(ACTcpClient client)
    {
        _finishVote = 1;
    }
    
    internal void CancelVote(ACTcpClient client)
    {
        _finishVote = -1;
    }
    
    internal void ExtendVote(ACTcpClient client, int seconds)
    {
        _extendVotingSeconds += seconds;
    }

    private async Task<bool> WaitVoting(CancellationToken stoppingToken)
    {
        try
        {
            // Wait for the vote to finish
            _votingOpen = true;

            for (var s = 0; s <= _configuration.VotingDurationSeconds; s++)
            {
                if (_finishVote != 0)
                    break;
                await Task.Delay(1000, stoppingToken);
            }

            // Allow to extend voting
            while (_extendVotingSeconds != 0)
            {
                var extend = _extendVotingSeconds;
                _extendVotingSeconds = 0;
                for (var s = 0; s <= extend; s++)
                {
                    if (_finishVote != 0)
                        break;
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException ex) { }
        finally
        {
            _votingOpen = false;
        }
        var result = _finishVote >= 0;
        _finishVote = 0;
        return result;
    }

    private async Task VotingAsync(CancellationToken stoppingToken, bool manualVote = false)
    {
        if(_voteStarted) return;
        
        _voteStarted = true;
        
        var last = _presetManager.CurrentPreset;

        _availablePresets.Clear();
        _alreadyVoted.Clear();
        _manualTrackChange = false;

        // Don't start votes if there is not available tracks for voting
        var tracksLeft = new List<PresetType>(_votePresets);
        tracksLeft.RemoveAll(t => t.Equals(last.Type!));
        if (tracksLeft.Count <= 1)
        {
            Log.Warning($"Not enough presets to start vote.");
            return;
        }

        if (_configuration.VoteEnabled || manualVote)
            _entryCarManager.BroadcastPacket(new ChatMessage { SessionId = 255, Message = "Vote for next track:" });
        
        // Add "Stay on current track"
        if (_configuration.IncludeStayOnTrackVote)
        {
            _availablePresets.Add(new PresetChoice { Track = last.Type, Votes = 0 });
            if (_configuration.VoteEnabled || manualVote)
                _entryCarManager.BroadcastPacket(new ChatMessage
                    { SessionId = 255, Message = $" /vt 0 - Stay on current track." });
            
            
        }
        for (int i = _availablePresets.Count; i < _configuration.VoteChoices; i++)
        {
            if (tracksLeft.Count < 1)
                break;
            var nextTrack = tracksLeft[Random.Shared.Next(tracksLeft.Count)];
            _availablePresets.Add(new PresetChoice { Track = nextTrack, Votes = 0 });
            tracksLeft.Remove(nextTrack);

            if (_configuration.VoteEnabled || manualVote)
                _entryCarManager.BroadcastPacket(new ChatMessage
                    { SessionId = 255, Message = $" /vt {i} - {nextTrack.Name}" });
        }

        // Wait for the vote to finish
        if (_configuration.VoteEnabled || manualVote)
        {
            if (!await WaitVoting(stoppingToken))
                return;
        }

        int maxVotes = _availablePresets.Max(w => w.Votes);
        List<PresetChoice> tracks = _availablePresets.Where(w => w.Votes == maxVotes).ToList();

        var winner = tracks[Random.Shared.Next(tracks.Count)];

        if (last.Type!.Equals(winner.Track!) || (maxVotes == 0 && !_configuration.ChangeTrackWithoutVotes))
        {
            _entryCarManager.BroadcastPacket(new ChatMessage
            {
                SessionId = 255,
                Message = $"Track vote ended. Staying on track for {_configuration.CycleIntervalMinutes} more minutes."
            });
        }
        else
        {
            _entryCarManager.BroadcastPacket(new ChatMessage
                { SessionId = 255, Message = $"Track vote ended. Next track: {winner.Track!.Name} - {winner.Votes} votes" });
            _entryCarManager.BroadcastPacket(new ChatMessage
            {
                SessionId = 255, 
                Message = $"Track will change in {(_configuration.TransitionDurationSeconds < 60 ? 
                    $"{_configuration.TransitionDurationSeconds} second(s)" :
                    $"{(int)Math.Ceiling(_configuration.TransitionDurationSeconds / 60.0)} minute(s)")}."
            });

            // Delay the track switch by configured time delay
            await Task.Delay(_configuration.TransitionDurationMilliseconds, stoppingToken);

            _presetManager.SetTrack(new PresetData(last.Type, winner.Track)
            {
                TransitionDuration = _configuration.TransitionDurationSeconds,
            });
        }
        _voteStarted = false;
    }
    
    private async Task ExecuteAdminAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_adminTrackChange)
                {
                    if (_adminTrack != null && !_adminTrack.Type!.Equals(_adminTrack.UpcomingType!))
                    {
                        Log.Information($"Next track: {_adminTrack!.UpcomingType!.Name}");
                        _entryCarManager.BroadcastPacket(new ChatMessage
                            { SessionId = 255, Message = $"Next track: {_adminTrack!.UpcomingType!.Name}" });
                        _entryCarManager.BroadcastPacket(new ChatMessage
                        {
                            SessionId = 255, 
                            Message = $"Track will change in {(_configuration.TransitionDurationSeconds < 60 ? 
                                $"{_configuration.TransitionDurationSeconds} second(s)" :
                                $"{(int)Math.Ceiling(_configuration.TransitionDurationSeconds / 60.0)} minute(s)")}."
                        });

                        // Delay the track switch by configured time delay
                        await Task.Delay(_configuration.TransitionDurationMilliseconds, stoppingToken);

                        _adminTrackChange = false;
                        _presetManager.SetTrack(_adminTrack);
                        _adminTrack = null;
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during admin preset update");
            }
            finally
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
    
    private async Task ExecuteManualVotingAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_manualTrackChange)
                {
                    
                    Log.Information($"Starting track vote.");
                    await VotingAsync(stoppingToken, true);
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during voting track update");
            }
            finally
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
    
    private async Task ExecuteVotingAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_configuration.CycleIntervalMilliseconds - _configuration.VotingDurationMilliseconds,
                stoppingToken);
            try
            {
                Log.Information($"Starting track vote.");
                await VotingAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during voting track update");
            }
            finally { }
        }
    }
}
