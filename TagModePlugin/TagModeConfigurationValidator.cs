using FluentValidation;

namespace TagModePlugin;

public class TagModeConfigurationValidator : AbstractValidator<TagModeConfiguration>
{
    public TagModeConfigurationValidator()
    {
        RuleFor(cfg => cfg.SessionPauseIntervalMinutes).GreaterThanOrEqualTo(1);
        RuleFor(cfg => cfg.SessionDurationMinutes).GreaterThanOrEqualTo(1);
    }
}
