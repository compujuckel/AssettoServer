using AssettoServer.Server.Configuration;
using JetBrains.Annotations;

namespace CustomCommandsPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class CustomCommandsConfiguration : IValidateConfiguration<CustomCommandsConfigurationValidator>
{
    public string DiscordURL { get; init; } = string.Empty;
}
