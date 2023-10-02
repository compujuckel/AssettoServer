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
# Should content manager download links be updated
UpdateContentManager: true
# Tracks that can be voted on
AvailableTracks:
- DisplayName: Gunsai
  TrackFolder: some/path/to/gunsai
  TrackLayoutConfig: GunsaiTogue
  ContentManagerLink: https://mega.nz/...... # field only required with UpdateContentManager: true
- DisplayName: Shutoko
  TrackFolder: some/path/to/Shutoko
  TrackLayoutConfig: Default
  ContentManagerLink: https://mega.nz/...... # field only required with UpdateContentManager: true

```
