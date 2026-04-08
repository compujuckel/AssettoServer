using FluentValidation;

namespace TimeDilationPlugin;

public class TimeDilationConfigurationValidator : AbstractValidator<TimeDilationConfiguration>
{
    public TimeDilationConfigurationValidator()
    {
        RuleFor(cfg => cfg.SunAngleLookupTable).NotEmpty().Unless(cfg => cfg.TimeLookupTable.Count > 0);
        RuleForEach(cfg => cfg.SunAngleLookupTable).ChildRules(salut =>
        {
            salut.RuleFor(s => s.TimeMult).GreaterThanOrEqualTo(0);
        });
        
        RuleFor(cfg => cfg.TimeLookupTable).NotEmpty().Unless(cfg => cfg.SunAngleLookupTable.Count > 0);
        RuleForEach(cfg => cfg.TimeLookupTable).ChildRules(tlut =>
        {
            tlut.RuleFor(t => t.Time).Matches(@"^(?:1?\d|2[0-3]):(?:[0-5]\d)$");
            tlut.RuleFor(t => t.TimeMult).GreaterThanOrEqualTo(0);
        });
    }
}
