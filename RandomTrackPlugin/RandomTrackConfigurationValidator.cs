using FluentValidation;
using JetBrains.Annotations;

namespace RandomTrackPlugin;

[UsedImplicitly]
public class RandomTrackConfigurationValidator : AbstractValidator<RandomTrackConfiguration>
{
    public RandomTrackConfigurationValidator()
    {
        RuleForEach(cfg => cfg.TrackWeights).ChildRules(ww =>
        {
            ww.RuleFor(w => w.Weight).GreaterThanOrEqualTo(0);
        });
        RuleFor(cfg => cfg.TrackDurationMinutes).GreaterThanOrEqualTo(5);
    }
}
