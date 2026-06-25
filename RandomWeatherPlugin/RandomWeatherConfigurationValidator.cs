using FluentValidation;
using AssettoServer.Shared.Weather;

namespace RandomWeatherPlugin;

public class RandomWeatherConfigurationValidator : AbstractValidator<RandomWeatherConfiguration>
{
    public RandomWeatherConfigurationValidator()
    {
        RuleFor(cfg => cfg.MinWeatherDurationMinutes).LessThanOrEqualTo(cfg => cfg.MaxWeatherDurationMinutes);
        RuleFor(cfg => cfg.MinTransitionDurationSeconds).LessThanOrEqualTo(cfg => cfg.MaxTransitionDurationSeconds);
        When(cfg => cfg.Mode == RandomWeatherMode.TransitionTable, () =>
        {
            RuleFor(cfg => cfg.WeatherTransitions)
                .NotNull()
                .DependentRules(() =>
                {
                    RuleFor(cfg => cfg.WeatherTransitions)
                        .NotEmpty();
                    RuleForEach(cfg => cfg.WeatherTransitions)
                        .Must(x => x.Value.Values.Any(v => v > 0))
                        .WithMessage("Each WeatherTransitions entry must contain at least one destination weather with a weight greater than 0");
                    RuleFor(cfg => cfg.WeatherTransitions)
                        .Must(wt =>
                        {
                            var destinations = wt.Values.SelectMany(d => d.Keys).Distinct();
                            return destinations.All(d => d == WeatherFxType.None || wt.ContainsKey(d));
                        }).WithMessage("Every weather that can be transitioned to must also have its own WeatherTransitions section");
                });
        });
        When(cfg => cfg.Mode == RandomWeatherMode.Default, () =>
        {
            RuleFor(cfg => cfg.WeatherWeights)
                .NotNull()
                .DependentRules(() =>
                {
                    RuleFor(cfg => cfg.WeatherWeights)
                        .Must(ww => ww.Values.Any(v => v > 0))
                        .WithMessage("At least one entry in WeatherWeights must have a weight greater than 0");
                    RuleForEach(cfg => cfg.WeatherWeights)
                        .ChildRules(ww => { ww.RuleFor(w => w.Value).GreaterThanOrEqualTo(0); });
                    RuleFor(cfg => cfg.WeatherWeights)
                        .Must(ww => !ww.ContainsKey(WeatherFxType.None) || ww[WeatherFxType.None] <= 0)
                        .WithMessage("WeatherFX Type \"None\" cannot be used as a weather weight");
                });
        });
    }
}
