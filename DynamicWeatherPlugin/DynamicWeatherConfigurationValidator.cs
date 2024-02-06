using FluentValidation;
using JetBrains.Annotations;

namespace DynamicWeatherPlugin;

[UsedImplicitly]
public class DynamicWeatherConfigurationValidator : AbstractValidator<DynamicWeatherConfiguration>
{
    public DynamicWeatherConfigurationValidator()
    {
        RuleForEach(cfg => cfg.WeatherTransitions).ChildRules(www =>
        {
            www.RuleForEach(ww => ww.Value).ChildRules(ww =>
            {
                ww.RuleFor(w => w.Value).GreaterThanOrEqualTo(0);
            });
        });
        RuleFor(cfg => cfg.MinWeatherDurationMinutes).LessThanOrEqualTo(cfg => cfg.MaxWeatherDurationMinutes);
        RuleFor(cfg => cfg.MinTransitionDurationSeconds).LessThanOrEqualTo(cfg => cfg.MaxTransitionDurationSeconds);
    }
}
