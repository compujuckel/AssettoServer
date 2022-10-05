using FluentValidation;
using JetBrains.Annotations;

namespace TimeDilationPlugin;

[UsedImplicitly]
public class TimeDilationConfigurationValidator : AbstractValidator<TimeDilationConfiguration>
{
    public TimeDilationConfigurationValidator()
    {
        RuleFor(cfg => cfg.LookupTable).NotEmpty();
        RuleForEach(cfg => cfg.LookupTable).ChildRules(lut =>
        {
            lut.RuleFor(l => l.TimeMult).GreaterThanOrEqualTo(0);
        });
    }
}
