# VotingTrackPlugin
Plugin to let players vote for a server weather at a specified interval.

## Configuration
Enable the plugin in `extra_cfg.yml`
```yaml
EnablePlugins:
- VotingTrackPlugin
```

Example configuration (add to bottom of `extra_cfg.yml`)
```yaml
---
!VotingTrackConfiguration
# Number of choices players can choose from at each voting interval
NumChoices: 3
# How long the vote stays open
VotingDurationSeconds: 300
# How often a vote takes place
VotingIntervalMinutes: 90
# Tracks that can be voted on
AvailableTracks:
- DisplayName: Gunsai
  TrackFolder: some/path/to/gunsai
  TrackLayoutConfig: GunsaiTogue
  Weight: 1.0
- DisplayName: Shutoko
  TrackFolder: some/path/to/Shutoko
  TrackLayoutConfig: Default
  Weight: 2.0
```
