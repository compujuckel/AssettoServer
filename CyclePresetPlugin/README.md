# CyclePresetPlugin

Plugin to let players vote for a server track at a specified interval.

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

#### Change track
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

#### Change to random track
- `/presetrandom`

## Configuration

Enable the plugin in `extra_cfg.yml`

```yaml
EnablePlugins:
- CyclePresetPlugin
```

Works best with `EnableClientMessages: true`.

Example configuration (write to `plugin_cycle_preset_cfg.yml`)

```yaml
---
!CyclePresetConfiguration
# Reconnect clients instead of kicking when restart is initiated.
# Please disable reconnect with varying entry lists in the presets
ReconnectEnabled: true
# Enable Voting
VoteEnabled: true
# Number of choices players can choose from at each voting interval
VoteChoices: 3
# Will track change randomly if no vote has been counted
ChangeTrackWithoutVotes: true
# Whether the current preset/track should be part of the next vote.
IncludeStayOnTrackVote: true
# How long the vote stays open
# Minimum 30, Default 300
VotingDurationSeconds: 300
# How often a cycle/vote takes place
# Minimum 5, Default 90
CycleIntervalMinutes: 90
# How long it takes to change the preset/track after warning about it 
# Minimum 1, Default 5
TransitionDurationMinutes: 10
```

### Presets

Create a folder `presets` in the directory of `AssettoServer.exe`.

Create copies of the `cfg` folder within the `presets` folder.

Rename the copies of the `cfg` folder to something that represents the preset you are creating.
Something like `Shutoko_low_bhp` or `LA_Canyon_hypercars`... You get the Idea.

Within each of those folders you now have to change the `server_cfg.ini` to feature the correct `TRACK` and `TRACK_LAYOUT`.
You can also just use the `cfg` folder of newly created presets from ContentManager.
Add the following file to each `presets` folder and change the values accordingly: `preset_cfg.yml`
Add this file into `cfg` as well.

Example preset configuration (write to `preset_cfg.yml`)
```yaml  
# The name of the Track; You will see this when voting
Name: Shutoko Cut Up
# Settings for Plugin features.
# Set Enabled to false, to exclude the Preset from Plugin Track lists
RandomTrack:
  Weight: 1.0
  Enabled: false
VotingTrack:
  Enabled: true
```

### Credits
- Code - @thisguyStan
- Lua Help - @tuttertep
- Reconnect Image - discord@Bethuel
