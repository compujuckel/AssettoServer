# RandomWeatherPlugin
Plugin that changes weather randomly.

**Important:** For smooth weather transitions and rain you need to set `EnableWeatherFx` to `true` in `extra_cfg.yml`.
## Configuration
Enable the plugin in `extra_cfg.yml`
```yaml
EnablePlugins:
- RandomWeatherPlugin
```

Example configuration (add to bottom of `extra_cfg.yml`)  
For a list of weather types that can be used with `WeatherWeights` see [WeatherFX Types](https://github.com/compujuckel/AssettoServer/wiki/WeatherFX-Types)
```yaml
---
!RandomWeatherConfiguration
# Weights for random weather selection, setting a weight to 0 blacklists a weather, default weight is 1.
WeatherWeights:
  LightThunderstorm: 2.0
  Thunderstorm: 0.0
  Hurricane: 0.5
# Minimum duration until next weather change
MinWeatherDurationMinutes: 15
# Maximum duration until next weather change
MaxWeatherDurationMinutes: 60
# Minimum weather transition duration
MinTransitionDurationSeconds: 180
# Maximum weather transition duration
MaxTransitionDurationSeconds: 600
```
