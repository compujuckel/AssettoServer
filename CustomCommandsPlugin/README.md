# CustomCommandsPlugin
Custom chat commands plugin, allows commands such as /discord to provide custom URLs in chat.

## Configuration
Enable the plugin in `extra_cfg.yml`
```yaml
EnablePlugins:
- CustomCommandsPlugin
```
Example configuration (add to bottom of `extra_cfg.yml`)
```yaml
---
!CustomCommandsConfiguration
DiscordURL: <URL to Discord channel>
```
