using FluentValidation;
using JetBrains.Annotations;

namespace CyclePresetPlugin;

[UsedImplicitly]
public class CyclePresetConfigurationValidator : AbstractValidator<CyclePresetConfiguration>
{
    public CyclePresetConfigurationValidator()
    {
        RuleFor(cfg => cfg.VoteChoices).GreaterThanOrEqualTo(2);
        RuleFor(cfg => cfg.CycleIntervalMinutes).GreaterThanOrEqualTo(5);
        RuleFor(cfg => cfg.VotingDurationSeconds).GreaterThanOrEqualTo(30);
        RuleFor(cfg => cfg.TransitionDurationSeconds).GreaterThanOrEqualTo(1);
    }
}
