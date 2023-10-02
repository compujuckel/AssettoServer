using FluentValidation;
using JetBrains.Annotations;

namespace VotingTrackPlugin;

[UsedImplicitly]
public class VotingTrackConfigurationValidator : AbstractValidator<VotingTrackConfiguration>
{
    public VotingTrackConfigurationValidator()
    {
        RuleFor(cfg => cfg.AvailableTracks).NotNull().Must(x => x.Count >= 2);
        RuleFor(cfg => cfg.NumChoices).GreaterThanOrEqualTo(2);
        RuleFor(cfg => cfg.VotingIntervalMinutes).GreaterThanOrEqualTo(1);
        RuleFor(cfg => cfg.VotingDurationSeconds).GreaterThanOrEqualTo(1);
    }
}
