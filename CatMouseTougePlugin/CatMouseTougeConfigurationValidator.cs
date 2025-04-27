using FluentValidation;
using JetBrains.Annotations;

namespace CatMouseTougePlugin;

// Use FluentValidation to validate plugin configuration
[UsedImplicitly]
public class CatMouseTougeConfigurationValidator : AbstractValidator<CatMouseTougeConfiguration>
{
    public CatMouseTougeConfigurationValidator()
    {
        // Validate that each value in the CarPerformanceRatings dictionary is between 1 and 1000
        RuleFor(cfg => cfg.CarPerformanceRatings)
            .Must(BeWithinValidRange)
            .WithMessage(x => GetInvalidRangeMessage(x.CarPerformanceRatings));

        // If you want to add additional constraints on the value
        RuleFor(cfg => cfg.MaxEloGain)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MaxEloGain must be a non-negative integer");
    }

    private bool BeWithinValidRange(Dictionary<string, int> ratings)
    {
        return ratings.All(kvp => kvp.Value >= 1 && kvp.Value <= 1000);
    }

    private string GetInvalidRangeMessage(Dictionary<string, int> ratings)
    {
        var invalidEntries = ratings
            .Where(kvp => kvp.Value < 1 || kvp.Value > 1000)
            .Select(kvp => $"Car '{kvp.Key}' has performance rating {kvp.Value}")
            .ToList();

        return $"The following car performance ratings must be between 1 and 1000: {string.Join(", ", invalidEntries)}";
    }
}
