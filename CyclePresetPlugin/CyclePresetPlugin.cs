using System.Reflection;
using AssettoServer.Commands;
using AssettoServer.Commands.Contexts;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Network.Packets.Shared;
using AssettoServer.Shared.Services;
using CyclePresetPlugin.Preset;
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
    private PresetData? _adminPreset = null;
    private bool _adminChange = false;
    
    private bool _manualChange = false;
    private bool _voteStarted = false;
    private int _extendVotingSeconds = 0;
    private short _finishVote = 0;

    private class PresetChoice
    {
        public PresetType? Preset { get; init; }
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
        
        _presetManager.SetPreset(new PresetData(presetConfigurationManager.CurrentConfiguration.ToPresetType(), null)
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

    internal void ListAllPresets(BaseCommandContext context)
    {
        context.Reply("List of all presets:");
        for (int i = 0; i < _adminPresets.Count; i++)
        {
            var pt = _adminPresets[i];
            context.Reply($" /presetuse {i} - {pt.Name}");
        }
    }

    internal void GetPreset(BaseCommandContext context)
    {
        Log.Information("Current preset: {Name} - {PresetFolder}", _presetManager.CurrentPreset.Type!.Name, _presetManager.CurrentPreset.Type!.PresetFolder);
        context.Reply($"Current preset: {_presetManager.CurrentPreset.Type!.Name} - {_presetManager.CurrentPreset.Type!.PresetFolder}");
    }

    internal void SetPreset(BaseCommandContext context, int choice)
    {
        var last = _presetManager.CurrentPreset;

        if (choice < 0 && choice >= _adminPresets.Count)
        {
            Log.Information("Invalid preset choice");
            context.Reply("Invalid preset choice.");

            return;
        }

        var next = _adminPresets[choice];

        if (last.Type!.Equals(next))
        {
            Log.Information("No change made, admin tried setting the current preset");
            context.Reply("No change made, you tried setting the current preset.");
        }
        else
        {
            _adminPreset = new PresetData(_presetManager.CurrentPreset.Type, next)
            {
                TransitionDuration = _configuration.TransitionDurationSeconds,
            };
            _adminChange = true;
        }
    }
    
    internal void RandomPreset(BaseCommandContext context)
    {
        var last = _presetManager.CurrentPreset;

        PresetType next;
        do
        {
            next = _adminPresets[Random.Shared.Next(_adminPresets.Count)];
        } while (last.Type!.Equals(next));

        _adminPreset = new PresetData(_presetManager.CurrentPreset.Type, next)
        {
            TransitionDuration = _configuration.TransitionDurationSeconds,
        };
        _adminChange = true;
        context.Reply("Switching to random preset (if it's not the current one)");
    }

    internal void CountVote(ChatCommandContext context, int choice)
    {
        if (!_votingOpen)
        {
            context.Reply("There is no ongoing track vote.");
            return;
        }

        if (choice >= _availablePresets.Count || choice < 0)
        {
            context.Reply("Invalid choice.");
            return;
        }
        
        if (_alreadyVoted.Contains(context.Client))
        {
            context.Reply("You voted already.");
            return;
        }

        _alreadyVoted.Add(context.Client);

        var votedPreset = _availablePresets[choice];
        votedPreset.Votes++;

        context.Reply($"Your vote for {votedPreset.Preset!.Name} has been counted.");
    }

    internal void StartVote(BaseCommandContext context)
    {

        if (_voteStarted)
        {
            context.Reply("Vote already ongoing.");
            return;
        }
        _manualChange = true;
    }
    
    internal void FinishVote(BaseCommandContext context)
    {
        _finishVote = 1;
        context.Reply("Finishing vote.");
    }
    
    internal void CancelVote(BaseCommandContext context)
    {
        _finishVote = -1;
        context.Reply("Canceling vote.");
    }
    
    internal void ExtendVote(BaseCommandContext context, int seconds)
    {
        _extendVotingSeconds += seconds;
        context.Reply($"Extending vote for {seconds} more seconds.");
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
        catch (OperationCanceledException ex) { 
            Log.Error(ex, "Error while waiting for preset votes");}
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
        _manualChange = false;

        // Don't start votes if there is not available presets for voting
        var presetsLeft = new List<PresetType>(_votePresets);
        presetsLeft.RemoveAll(t => t.Equals(last.Type!));
        if (presetsLeft.Count <= 1)
        {
            Log.Warning("Not enough presets to start vote");
            return;
        }

        if (_configuration.VoteEnabled || manualVote)
            _entryCarManager.BroadcastPacket(new ChatMessage { SessionId = 255, Message = "Vote for next track:" });
        
        // Add "Stay on current track"
        if (_configuration.IncludeStayOnTrackVote)
        {
            _availablePresets.Add(new PresetChoice { Preset = last.Type, Votes = 0 });
            if (_configuration.VoteEnabled || manualVote)
                _entryCarManager.BroadcastPacket(new ChatMessage
                    { SessionId = 255, Message = $" /vt 0 - Stay on current track." });
            
            
        }
        for (int i = _availablePresets.Count; i < _configuration.VoteChoices; i++)
        {
            if (presetsLeft.Count < 1)
                break;
            var nextPreset = presetsLeft[Random.Shared.Next(presetsLeft.Count)];
            _availablePresets.Add(new PresetChoice { Preset = nextPreset, Votes = 0 });
            presetsLeft.Remove(nextPreset);

            if (_configuration.VoteEnabled || manualVote)
                _entryCarManager.BroadcastPacket(new ChatMessage
                    { SessionId = 255, Message = $" /vt {i} - {nextPreset.Name}" });
        }

        // Wait for the vote to finish
        if (_configuration.VoteEnabled || manualVote)
        {
            if (!await WaitVoting(stoppingToken))
                return;
        }

        int maxVotes = _availablePresets.Max(w => w.Votes);
        List<PresetChoice> presets = _availablePresets.Where(w => w.Votes == maxVotes).ToList();

        var winner = presets[Random.Shared.Next(presets.Count)];

        if (last.Type!.Equals(winner.Preset!) || (maxVotes == 0 && !_configuration.ChangePresetWithoutVotes))
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
                { SessionId = 255, Message = $"Track vote ended. Next track: {winner.Preset!.Name} - {winner.Votes} votes" });
            _entryCarManager.BroadcastPacket(new ChatMessage
            {
                SessionId = 255, 
                Message = $"Track will change in {(_configuration.TransitionDurationSeconds < 60 ? 
                    $"{_configuration.TransitionDurationSeconds} second(s)" :
                    $"{(int)Math.Ceiling(_configuration.TransitionDurationSeconds / 60.0)} minute(s)")}."
            });

            // Delay the preset switch by configured time delay
            await Task.Delay(_configuration.DelayTransitionDurationMilliseconds, stoppingToken);

            _presetManager.SetPreset(new PresetData(last.Type, winner.Preset)
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
                if (_adminChange)
                {
                    if (_adminPreset != null && !_adminPreset.Type!.Equals(_adminPreset.UpcomingType!))
                    {
                        Log.Information("Next preset: {Preset}", _adminPreset!.UpcomingType!.Name);
                        _entryCarManager.BroadcastPacket(new ChatMessage
                            { SessionId = 255, Message = $"Next track: {_adminPreset!.UpcomingType!.Name}" });
                        _entryCarManager.BroadcastPacket(new ChatMessage
                        {
                            SessionId = 255, 
                            Message = $"Track will change in {(_configuration.TransitionDurationSeconds < 60 ? 
                                $"{_configuration.TransitionDurationSeconds} second(s)" :
                                $"{(int)Math.Ceiling(_configuration.TransitionDurationSeconds / 60.0)} minute(s)")}."
                        });

                        // Delay the preset switch by configured time delay
                        await Task.Delay(_configuration.DelayTransitionDurationMilliseconds, stoppingToken);

                        _adminChange = false;
                        _presetManager.SetPreset(_adminPreset);
                        _adminPreset = null;
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
                if (_manualChange)
                {
                    
                    Log.Information("Starting preset vote");
                    await VotingAsync(stoppingToken, true);
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during voting preset update");
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
                Log.Information("Starting preset vote");
                await VotingAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during voting preset update");
            }
            finally { }
        }
    }
}
