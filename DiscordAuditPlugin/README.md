# DiscordAuditPlugin
## Features
* Send a Discord message on every kick/ban
* Log all chat messages to a Discord channel

Generate Webhook URLs: https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks
## Configuration
Enable the plugin in `extra_cfg.yml`
```yaml
EnablePlugins:
  - DiscordAuditPlugin
```
Example configuration (add to bottom of `extra_cfg.yml`)
```yaml
---
!DiscordConfiguration
# Avatar picture URL
PictureUrl: 
# Discord webhook URL for kick/ban events
AuditUrl: https://discord.com/api/webhooks/...
# Discord webhook URL for chat messages
ChatUrl: https://discord.com/api/webhooks/...
```