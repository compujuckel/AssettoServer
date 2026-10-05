using FluentValidation;

namespace AutoModerationPlugin;

public class AutoModerationConfigurationValidator : AbstractValidator<AutoModerationConfiguration>
{
    public AutoModerationConfigurationValidator()
    {
        RuleFor(cfg => cfg.AfkPenalty).NotNull().ChildRules(ap =>
        {
            ap.RuleFor(a => a.DurationMinutes).GreaterThanOrEqualTo(1);
        });
        RuleFor(cfg => cfg.HighPingPenalty).NotNull().ChildRules(hpp =>
        {
            hpp.RuleFor(h => h.DurationSeconds).GreaterThanOrEqualTo(0);
            hpp.RuleFor(h => h.MaximumPingMilliseconds).GreaterThanOrEqualTo(0);
        });
        RuleFor(cfg => cfg.WrongWayPenalty).NotNull().ChildRules(wwp =>
        {
            wwp.RuleFor(w => w.DurationSeconds).GreaterThanOrEqualTo(0);
            wwp.RuleFor(w => w.MinimumSpeedKph).GreaterThanOrEqualTo(0);
            wwp.RuleFor(w => w.PitsBeforeKick).GreaterThanOrEqualTo(0);
        });
        RuleFor(cfg => cfg.NoLightsPenalty).NotNull().ChildRules(nlp =>
        {
            nlp.RuleFor(n => n.DurationSeconds).GreaterThanOrEqualTo(0);
            nlp.RuleFor(n => n.MinimumSpeedKph).GreaterThanOrEqualTo(0);
            nlp.RuleFor(n => n.PitsBeforeKick).GreaterThanOrEqualTo(0);
            nlp.RuleFor(n => n.IgnoreSeconds).GreaterThanOrEqualTo(0);
        });
        RuleFor(cfg => cfg.BlockingRoadPenalty).NotNull().ChildRules(brp =>
        {
            brp.RuleFor(b => b.DurationSeconds).GreaterThanOrEqualTo(0);
            brp.RuleFor(b => b.MaximumSpeedKph).GreaterThanOrEqualTo(0);
            brp.RuleFor(b => b.PitsBeforeKick).GreaterThanOrEqualTo(0);
        });
    }
}
