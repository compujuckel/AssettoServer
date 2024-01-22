# VotingPresetPlugin

Plugin to let players vote for a preset/track at a specified interval.

## Commands
Most commands have multiple alias

#### Show current track / preset
- `/currenttrack`
- `/presetshow`
- `/currentpreset`

#### Vote for the next track / preset
Server will ask users to vote for new map as per configured timeframe.
- `/votetrack <number>`
- `/vt <number>`
- `/votepreset <number>`
- `/vp <number>`
- `/presetvote <number>`
- `/pv <number>`

### Admin commands

#### List available tracks / presets
- `/presetlist`
- `/presetget`
- `/presets`

#### Change track / preset
Exact usage is shown by track list
- `/presetset`
- `/presetchange`
- `/presetuse`
- `/presetupdate`

#### Initiate track / preset vote
- `/presetstartvote`
- `/presetvotestart`

#### Finish track / preset vote
- `/presetfinishvote`
- `/presetvotefinish`

#### Cancel track / preset vote
- `/presetcancelvote`
- `/presetvotecancel`

#### Change to random track / preset with equal odds
- `/presetrandom`

## Configuration

Enable the plugin in `extra_cfg.yml`

```yaml
EnablePlugins:
- VotingPresetPlugin
```

Works best with `EnableClientMessages: true`.

Example configuration (write to `plugin_voting_preset_cfg.yml`)

```yaml
# Reconnect clients instead of kicking when restart is initiated. 
# Please disable reconnect with varying entry lists in the presets
ReconnectEnabled: true
# Enable Voting
VoteEnabled: true
# Number of choices players can choose from at each voting interval
VoteChoices: 3
# Will track change randomly if no vote has been counted
ChangePresetWithoutVotes: false
# Whether the current preset/track should be part of the next vote.
IncludeStayOnTrackVote: true
# How often a vote takes place. Minimum 5, Default 90
VotingIntervalMinutes: 90
# How long the vote stays open. Minimum 30, Default 300
VotingDurationSeconds: 300
# How long it takes to change the preset/track after notifying. Minimum 1, Default 5
TransitionDurationSeconds: 5
# How long it takes before notifying. Minimum 0, Default 10
DelayTransitionDurationSeconds: 10
# Preset specific settings 
# The cfg/ directory is always ignored for the presets pool.
Meta:
    # The name that is displayed when a vote is going on or the preset is changing
    Name: Shutoko noises
     # Preset specific settings for voting
    Voting:
        # Is this preset part of the voting
        Enabled: true
```

### Presets

Create a folder `presets` in the directory of `AssettoServer.exe`.

Create copies of the `cfg` folder within the `presets` folder.

Rename the copies of the `cfg` folder to something that represents the preset you are creating.
Something like `Shutoko_low_bhp` or `LA_Canyon_hypercars`... You get the Idea.

Within each of those folders you now have to change the `server_cfg.ini` to feature the correct `TRACK` and `TRACK_LAYOUT`.<br>
You can also just use the `cfg` folder of newly created presets from ContentManager.<br>
Don't forget to add the `extra_cfg.yml` and other plugin config files to each `presets` folder and change the values accordingly.

### Credits
- Code - @thisguyStan
- Lua Help - @tuttertep
- Reconnect Image - discord@bethuel
