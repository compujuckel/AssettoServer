using FluentValidation;

namespace VotingWeatherPlugin;

public class VotingWeatherConfigurationValidator : AbstractValidator<VotingWeatherConfiguration>
{
    public VotingWeatherConfigurationValidator()
    {
        RuleFor(cfg => cfg.BlacklistedWeathers).NotNull();
        RuleFor(cfg => cfg.NumChoices).GreaterThanOrEqualTo(2);
        RuleFor(cfg => cfg.VotingIntervalMinutes).GreaterThanOrEqualTo(1);
        RuleFor(cfg => cfg.VotingDurationSeconds).GreaterThanOrEqualTo(1);
    }
}
