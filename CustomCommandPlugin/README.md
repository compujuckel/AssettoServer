# CustomCommandPlugin
## Features
* Create custom commands

## Configuration
Enable the plugin in `extra_cfg.yml`
```yaml
EnablePlugins:
  - CustomCommandPlugin
```
Example configuration (`plugin_custom_command_cfg.yml`)
```yaml
# Configure your custom commands
Commands:
    alias: some spicy response
    discord: https://discord.gg/uXEXRcSkyz
    docs: https://assettoserver.org/
```

## Usage
In-game you will be able to use the configured commands with the chat app.

By typing `/alias` you will get the response `some spicy response`

Only the person sending the command will get the response from the server.
