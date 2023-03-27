using FluentValidation;
using JetBrains.Annotations;

namespace CustomCommandsPlugin;

[UsedImplicitly]
public class CustomCommandsConfigurationValidator : AbstractValidator<CustomCommandsConfiguration>
{
    public CustomCommandsConfigurationValidator()
    {
        RuleFor(cfg => cfg.DiscordURL).NotNull();
    }
}
