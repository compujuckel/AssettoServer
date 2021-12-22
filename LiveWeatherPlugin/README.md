# LiveWeatherPlugin
Plugin to get realtime weather from OpenWeatherMap.

**Important:** For smooth weather transitions and rain you need to set `EnableWeatherFx` to `true` in `extra_cfg.yml`.

To get your API key a free account on https://openweathermap.org/ is required.
## Configuration
Enable the plugin in `extra_cfg.yml`
```yaml
EnablePlugins:
- LiveWeatherPlugin
```
Example configuration (add to bottom of `extra_cfg.yml`)
```yaml
---
!LiveWeatherConfiguration
OpenWeatherMapApiKey: <your api key here>
UpdateIntervalMinutes: 10
```