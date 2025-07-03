using FluentValidation;

namespace VotingPresetPlugin;

public class VotingPresetConfigurationValidator : AbstractValidator<VotingPresetConfiguration>
{
    public VotingPresetConfigurationValidator()
    {
        RuleFor(cfg => cfg.VoteChoices).GreaterThanOrEqualTo(2);
        RuleFor(cfg => cfg.IntervalMinutes).GreaterThanOrEqualTo(5);
        RuleFor(cfg => cfg.VotingDurationSeconds).GreaterThanOrEqualTo(10);
        RuleFor(cfg => cfg.TransitionDurationSeconds).GreaterThanOrEqualTo(2);
        RuleFor(cfg => cfg.TransitionDelaySeconds).GreaterThanOrEqualTo(0);
    }
}
