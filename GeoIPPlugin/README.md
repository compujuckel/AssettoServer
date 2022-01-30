# GeoIPPlugin
Plugin to do GeoIP lookups for connected players.

**Important:** Lookups are done locally, so you need a copy of the GeoLite2 city database which you can download here: https://dev.maxmind.com/geoip/geolite2-free-geolocation-data?lang=en

Log output format:
```
GeoIP results for {ClientName}: {Country} ({CountryCode}) [{Lat},{Lon}]
```

## Configuration
Enable the plugin in `extra_cfg.yml`
```yaml
EnablePlugins:
- GeoIPPlugin
```
Example configuration (add to bottom of `extra_cfg.yml`)
```yaml
---
!GeoIPConfiguration
DatabasePath: <path to GeoLite2-City.mmdb>
```