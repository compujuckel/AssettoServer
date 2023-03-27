using AssettoServer.Commands;
using Qmmands;

namespace CustomCommandsPlugin;

public class CustomCommandsCommandModule : ACModuleBase
{
    private readonly CustomCommandsConfiguration _configuration;

    public CustomCommandsCommandModule(CustomCommandsConfiguration configuration)
    {
        _configuration = configuration;
    }

    [Command("discord")]
    public void DiscordLink()
    {
        Reply($"Discord link: [{_configuration.DiscordURL}]");
    }
}
