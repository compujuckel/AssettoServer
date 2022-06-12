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
For a list of weather types that can be used with `BlacklistedWeathers` see [WeatherFX Types](https://github.com/compujuckel/AssettoServer/wiki/WeatherFX-Types)
```yaml
---
!RandomWeatherConfiguration
# List of weathers that won't be chosen
BlacklistedWeathers:
- LightThunderstorm
- Thunderstorm
- HeavyThunderstorm
- LightDrizzle
- Drizzle
- HeavyDrizzle
- LightRain
- Rain
- HeavyRain
- LightSleet
- Sleet
- HeavySleet
- Hail
- Tornado
- Hurricane
# Minimum duration until next weather change
MinWeatherDurationMinutes: 15
# Maximum duration until next weather change
MaxWeatherDurationMinutes: 60
# Minimum weather transition duration
MinTransitionDurationSeconds: 180
# Maximum weather transition duration
MaxTransitionDurationSeconds: 600
```
