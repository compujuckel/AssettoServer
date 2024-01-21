# AutoModerationPlugin

Plugin to automatically kick players when they violate rules:
* Driving without lights during the night
* Driving the wrong way
* Blocking the road

Admins are exempt from these rules.

When `EnableClientMessages` is enabled, wrong way / no parking / no lights signs will be shown to the player.  
Included in the `Flags` folder are Japanese-style signs. You can replace these with custom signs.

## Configuration
Enable the plugin in `extra_cfg.yml`
```yaml
EnablePlugins:
- AutoModerationPlugin
```

Example configuration (add to bottom of `extra_cfg.yml`)
```yaml
---
!AutoModerationConfiguration
# Kick players driving without lights during the night
NoLightsKick:
  # Set to false to disable
  Enabled: true
  # Time in which no warning or signs will be sent
  IgnoreSeconds: 2
  # Time after the player gets kicked. A warning will be sent in chat after half this time
  DurationSeconds: 60
  # Players driving slower than this speed will not be kicked
  MinimumSpeedKph: 20
  # The amount of times a player will be send to pits before being kicked
  PitsBeforeKick: 3
# Kick players driving the wrong way. AI has to enabled for this to work
WrongWayKick:
  # Set to false to disable
  Enabled: true
  # Time after the player gets kicked. A warning will be sent in chat after half this time
  DurationSeconds: 20
  # Players driving slower than this speed will not be kicked
  MinimumSpeedKph: 20
  # The amount of times a player will be send to pits before being kicked
  PitsBeforeKick: 3
# Kick players blocking the road. AI has to be enabled for this to work
BlockingRoadKick:
  # Set to false to disable
  Enabled: true
  # Time after the player gets kicked. A warning will be sent in chat after half this time
  DurationSeconds: 30
  # Players driving faster than this speed will not be kicked
  MaximumSpeedKph: 5
  # The amount of times a player will be send to pits before being kicked
  PitsBeforeKick: 3
```
