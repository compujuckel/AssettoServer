using FluentValidation;
using JetBrains.Annotations;

namespace LiveWeatherPlugin;

[UsedImplicitly]
public class LiveWeatherConfigurationValidator : AbstractValidator<LiveWeatherConfiguration>
{
    public LiveWeatherConfigurationValidator()
    {
        RuleFor(cfg => cfg.OpenWeatherMapApiKey).NotEmpty().Matches("[0-9a-f]+");
        RuleFor(cfg => cfg.UpdateIntervalMinutes).GreaterThanOrEqualTo(1);
    }
}
