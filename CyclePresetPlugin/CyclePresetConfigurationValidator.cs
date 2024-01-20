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
        RuleFor(cfg => cfg.VotingDurationSeconds).GreaterThanOrEqualTo(10);
        RuleFor(cfg => cfg.TransitionDurationSeconds).GreaterThanOrEqualTo(2);
        RuleFor(cfg => cfg.DelayTransitionDurationSeconds).GreaterThanOrEqualTo(0);
        
        RuleFor(cfg => cfg.Meta).ChildRules(meta =>
        {
            meta.RuleFor(m => m.Random).ChildRules(r =>
            {
                r.RuleFor(c => c!.Weight).GreaterThanOrEqualTo(0f);
            });
        });
    }
}
