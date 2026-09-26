using FluentValidation;

namespace LiveWeatherPlugin;

public class LiveWeatherConfigurationValidator : AbstractValidator<LiveWeatherConfiguration>
{
    public LiveWeatherConfigurationValidator()
    {
        RuleFor(cfg => cfg.OpenWeatherMapApiKey).NotEmpty().Matches("[0-9a-f]+");
        RuleFor(cfg => cfg.UpdateIntervalMinutes).GreaterThanOrEqualTo(1);
        RuleFor(cfg => cfg.TransitionDurationSeconds)
            .GreaterThanOrEqualTo(1)
            .Must((cfg, transition) => transition <= cfg.UpdateIntervalMinutes * 60)
            .WithMessage("Weather transition cannot be longer than the update interval");
    }
}
