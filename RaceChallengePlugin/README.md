# RaceChallengePlugin
Plugin to challenge other players to races.
## Configuration
Enable the plugin in `extra_cfg.yml`
```yaml
EnablePlugins:
  - RaceChallengePlugin
```
## How it works
You can drive behind another player controlled car and flash either your headlights or high beams three times to initiate a race challenge.
The challenged player can accept the race by turning on their hazard lights or by writing `/accept` into the chat. If the challenged player fails to do this within 10 seconds or the two cars are not close enough to each other the race challenge will be cancelled.

Both players will start the race with a fixed amount of points. The leading player will not lose any points while the player behind loses points each second. The further the following player is behind, the more points they will lose.

The race ends when a players point score is 0.