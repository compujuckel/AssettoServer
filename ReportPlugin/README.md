# ReportPlugin
Plugin to let players submit replays for review by server moderators.

It works like this:
* Player sends a replay clip to the server by pressing Ctrl+Shift+S (CSP 0.1.76+ required)
  * Replay is saved on the server in the folder `reports/`, along with a json file containing a list of connected players and events
* Player uses the `/report <reason>` command to specify a reason for the report
  * Discord message is sent to a specified webhook, containing the replay and json file as an attachment
* Moderators can review the replay and take appropriate action

## Configuration
Enable the plugin in `extra_cfg.yml`
```yaml
EnablePlugins:
- ReportPlugin
```

Example configuration (add to bottom of `extra_cfg.yml`)
```yaml
---
!ReportConfiguration
# Length of replay clips
ClipDurationSeconds: 60
# Discord webhook URL to send reports to. Optional, reports will be logged to the server log if you leave this empty
WebhookUrl: https://discord.com/api/webhooks/...
```
