# VotingWeatherPlugin
Plugin to let players vote for a server weather at a specified interval.

**Important:** For smooth weather transitions and rain you need to set `EnableWeatherFx` to `true` in `extra_cfg.yml`.
## Configuration
Enable the plugin in `extra_cfg.yml`
```yaml
EnablePlugins:
- VotingWeatherPlugin
```
Example configuration (append to `extra_cfg.yml`)
```yaml
---
!VotingWeatherConfiguration
# Number of choices players can choose from at each voting interval
NumChoices: 3
# How long the vote stays open
VotingDurationSeconds: 30
# How often a vote takes place
VotingIntervalMinutes: 10
# Weather types that can't be voted on
BlacklistedWeathers:
- None
- Cold
- Hot
- Windy
```