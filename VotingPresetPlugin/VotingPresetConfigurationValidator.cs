using FluentValidation;
using JetBrains.Annotations;

namespace VotingPresetPlugin;

[UsedImplicitly]
public class VotingPresetConfigurationValidator : AbstractValidator<VotingPresetConfiguration>
{
    public VotingPresetConfigurationValidator()
    {
        RuleFor(cfg => cfg.VoteChoices).GreaterThanOrEqualTo(2);
        RuleFor(cfg => cfg.VotingIntervalMinutes).GreaterThanOrEqualTo(5);
        RuleFor(cfg => cfg.VotingDurationSeconds).GreaterThanOrEqualTo(10);
        RuleFor(cfg => cfg.TransitionDurationSeconds).GreaterThanOrEqualTo(2);
        RuleFor(cfg => cfg.DelayTransitionDurationSeconds).GreaterThanOrEqualTo(0);
    }
}
