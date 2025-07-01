using System.Reflection;
using AssettoServer.Commands.Contexts;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;
using VotingPresetPlugin.Preset;

namespace VotingPresetPlugin;

public class VotingPresetPlugin : BackgroundService
{
    private readonly EntryCarManager _entryCarManager;
    private readonly PresetManager _presetManager;
    private readonly VotingPresetConfiguration _configuration;
    
    private readonly List<PresetType> _votePresets;
    private readonly List<ACTcpClient> _alreadyVoted = new();
    private readonly List<PresetChoice> _availablePresets = new();
    private bool _votingOpen = false;
    
    private readonly List<PresetType> _adminPresets;
    
    private bool _voteStarted = false;
    private int _extendVotingSeconds = 0;
    private short _finishVote = 0;
    private CancellationToken _cancellationToken = CancellationToken.None;

    private class PresetChoice
    {
        public PresetType? Preset { get; init; }
        public int Votes { get; set; }
    }

    public VotingPresetPlugin(VotingPresetConfiguration configuration,
        PresetConfigurationManager presetConfigurationManager, 
        ACServerConfiguration acServerConfiguration,
        EntryCarManager entryCarManager,
        PresetManager presetManager,
        CSPServerScriptProvider scriptProvider,
        CSPFeatureManager cspFeatureManager)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _presetManager = presetManager;

        _votePresets = presetConfigurationManager.VotingPresetTypes;
        _adminPresets = presetConfigurationManager.AllPresetTypes;
        
        if (acServerConfiguration.CSPTrackOptions.MinimumCSPVersion < CSPVersion.V0_2_0)
        {
            throw new ConfigurationException("VotingPresetPlugin needs a minimum required CSP version of 0.2.0 (2651)");
        }
        
        _presetManager.SetPreset(new PresetData(presetConfigurationManager.CurrentConfiguration.ToPresetType(), null)
        {
            IsInit = true,
            TransitionDuration = 0
        });
        
        if (acServerConfiguration.Extra.EnableClientMessages && _configuration.EnableReconnect)
        {
            scriptProvider.AddScript(Assembly.GetExecutingAssembly().GetManifestResourceStream("VotingPresetPlugin.lua.reconnectclient.lua")!, "reconnectclient.lua");
            
            cspFeatureManager.Add(new CSPFeature { Name = "FREQUENT_TRACK_CHANGES" });
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _cancellationToken = stoppingToken;
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_configuration.IntervalMilliseconds - _configuration.VotingDurationMilliseconds,
                stoppingToken);
            try
            {
                Log.Information("Starting preset vote");
                if (_configuration.EnableVote)
                    await VotingAsync(stoppingToken);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during voting preset update");
            }
        }
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
            context.Reply($"Switching to preset: {next.Name}");
            _ = AdminPreset(new PresetData(_presetManager.CurrentPreset.Type, next)
            {
                TransitionDuration = _configuration.TransitionDurationSeconds,
            });
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
        context.Reply($"Switching to random preset: {next.Name}");
        _ = AdminPreset(new PresetData(_presetManager.CurrentPreset.Type, next)
        {
            TransitionDuration = _configuration.TransitionDurationSeconds,
        });
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
        
        try
        {
            Log.Information("Starting preset vote");
            _ = VotingAsync(_cancellationToken, true);
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during voting preset update");
        }
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

        // Don't start votes if there is not available presets for voting
        var presetsLeft = new List<PresetType>(_votePresets);
        presetsLeft.RemoveAll(t => t.Equals(last.Type!));
        if (presetsLeft.Count <= 1)
        {
            Log.Warning("Not enough presets to start vote");
            return;
        }

        if (_configuration.EnableVote || manualVote)
            _entryCarManager.BroadcastChat("Vote for next track:");
        
        if (_configuration.EnableStayOnTrack)
        {
            _availablePresets.Add(new PresetChoice { Preset = last.Type, Votes = 0 });
            if (_configuration.EnableVote || manualVote)
            {
                _entryCarManager.BroadcastChat(" /vt 0 - Stay on current track.");
            }
        }
        for (int i = _availablePresets.Count; i < _configuration.VoteChoices; i++)
        {
            if (presetsLeft.Count < 1)
                break;
            var nextPreset = presetsLeft[Random.Shared.Next(presetsLeft.Count)];
            _availablePresets.Add(new PresetChoice { Preset = nextPreset, Votes = 0 });
            presetsLeft.Remove(nextPreset);

            if (_configuration.EnableVote || manualVote)
            {
                _entryCarManager.BroadcastChat($" /vt {i} - {nextPreset.Name}");
            }
        }

        if (_configuration.EnableVote || manualVote)
        {
            if (!await WaitVoting(stoppingToken))
                return;
        }

        int maxVotes = _availablePresets.Max(w => w.Votes);
        List<PresetChoice> presets = _availablePresets.Where(w => w.Votes == maxVotes).ToList();

        var winner = presets[Random.Shared.Next(presets.Count)];

        if (last.Type!.Equals(winner.Preset!) || (maxVotes == 0 && !_configuration.ChangePresetWithoutVotes))
        {
            _entryCarManager.BroadcastChat($"Track vote ended. Staying on track for {_configuration.IntervalMinutes} more minutes.");
        }
        else
        {
            _entryCarManager.BroadcastChat($"Track vote ended. Next track: {winner.Preset!.Name} - {winner.Votes} votes");
            _entryCarManager.BroadcastChat($"Track will change in {(_configuration.TransitionDelaySeconds < 60 ? 
                    $"{_configuration.TransitionDelaySeconds} second(s)" :
                    $"{(int)Math.Ceiling(_configuration.TransitionDelaySeconds / 60.0)} minute(s)")}.");

            await Task.Delay(_configuration.TransitionDelayMilliseconds, stoppingToken);

            _presetManager.SetPreset(new PresetData(last.Type, winner.Preset)
            {
                TransitionDuration = _configuration.TransitionDurationSeconds,
            });
        }
        _voteStarted = false;
    }
    
    private async Task AdminPreset(PresetData preset)
    {
        try
        {
            if (preset.Type!.Equals(preset.UpcomingType!)) return;
            Log.Information("Next preset: {Preset}", preset.UpcomingType!.Name);
            _entryCarManager.BroadcastChat($"Next track: {preset.UpcomingType!.Name}");
            _entryCarManager.BroadcastChat($"Track will change in {(_configuration.TransitionDelaySeconds < 60 ? 
                    $"{_configuration.TransitionDelaySeconds} second(s)" :
                    $"{(int)Math.Ceiling(_configuration.TransitionDelaySeconds / 60.0)} minute(s)")}.");

            await Task.Delay(_configuration.TransitionDelayMilliseconds);

            _presetManager.SetPreset(preset);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during admin preset update");
        }
    }
}
