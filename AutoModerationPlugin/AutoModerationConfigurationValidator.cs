using FluentValidation;
using JetBrains.Annotations;

namespace AutoModerationPlugin;

[UsedImplicitly]
public class AutoModerationConfigurationValidator : AbstractValidator<AutoModerationConfiguration>
{
    public AutoModerationConfigurationValidator()
    {
        RuleFor(cfg => cfg.WrongWayKick).NotNull().ChildRules(wwk =>
        {
            wwk.RuleFor(w => w.DurationSeconds).GreaterThanOrEqualTo(0);
            wwk.RuleFor(w => w.MinimumSpeedKph).GreaterThanOrEqualTo(0);
        });
        RuleFor(cfg => cfg.NoLightsKick).NotNull().ChildRules(nlk =>
        {
            nlk.RuleFor(n => n.DurationSeconds).GreaterThanOrEqualTo(0);
            nlk.RuleFor(n => n.MinimumSpeedKph).GreaterThanOrEqualTo(0);
        });
        RuleFor(cfg => cfg.BlockingRoadKick).NotNull().ChildRules(brk =>
        {
            brk.RuleFor(b => b.DurationSeconds).GreaterThanOrEqualTo(0);
            brk.RuleFor(b => b.MaximumSpeedKph).GreaterThanOrEqualTo(0);
        });
    }
}
