using FluentValidation;
using JetBrains.Annotations;

namespace TagModePlugin;

[UsedImplicitly]
public class TagModeConfigurationValidator : AbstractValidator<TagModeConfiguration>
{
    public TagModeConfigurationValidator()
    {
        RuleFor(cfg => cfg.SessionPauseIntervalMinutes).GreaterThanOrEqualTo(1);
        RuleFor(cfg => cfg.SessionDurationMinutes).GreaterThanOrEqualTo(1);
    }
}
