using FluentValidation;

namespace RandomWeatherPlugin;

public class RandomWeatherConfigurationValidator : AbstractValidator<RandomWeatherConfiguration>
{
    public RandomWeatherConfigurationValidator()
    {
        RuleForEach(cfg => cfg.WeatherWeights).ChildRules(ww =>
        {
            ww.RuleFor(w => w.Value).GreaterThanOrEqualTo(0);
        });
        RuleFor(cfg => cfg.MinWeatherDurationMinutes).LessThanOrEqualTo(cfg => cfg.MaxWeatherDurationMinutes);
        RuleFor(cfg => cfg.MinTransitionDurationSeconds).LessThanOrEqualTo(cfg => cfg.MaxTransitionDurationSeconds);
    }
}
