using System.Numerics;
using FluentValidation;
using JetBrains.Annotations;

namespace TougePlugin;

// Use FluentValidation to validate plugin configuration
[UsedImplicitly]
public class TougeConfigurationValidator : AbstractValidator<TougeConfiguration>
{
    public TougeConfigurationValidator()
    {
        // Validate that each value in the CarPerformanceRatings dictionary is between 1 and 1000
        RuleFor(cfg => cfg.CarPerformanceRatings)
            .Must(BeWithinValidRange)
            .WithMessage(x => GetInvalidRangeMessage(x.CarPerformanceRatings));

        RuleFor(cfg => cfg.MaxEloGain)
            .GreaterThan(0)
            .WithMessage("MaxEloGain must be a positive integer");

        RuleFor(cfg => cfg.StartingPositions)
            .Must(HaveValidStartingPositionPair)
            .WithMessage("There must be at least one pair of starting positions, each with 'Position' and 'Direction' keys.");
        
        RuleFor(cfg => cfg.ProvisionalRaces)
        .GreaterThan(0)
        .WithMessage("ProvisionalRaces must be a positive integer");

        RuleFor(cfg => cfg.MaxEloGainProvisional)
            .GreaterThan(0)
            .WithMessage("MaxEloGainProvisional must be a positive integer");

        RuleFor(cfg => cfg.outrunTime)
            .InclusiveBetween(1, 60)
            .WithMessage("OutrunTime must be an integer between 1 and 60 seconds.");

        RuleFor(cfg => cfg.postgresqlConnectionString)
            .NotEmpty()
            .WithMessage("PostgreSQL connection string must be provided when isDbLocalMode is false.")
            .When(cfg => !cfg.isDbLocalMode);
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

    private bool HaveValidStartingPositionPair(Dictionary<string, Vector3>[][] positions)
    {
        if (positions == null) return false;

        foreach (var group in positions)
        {
            if (group == null || group.Length != 2) continue;

            bool bothHaveKeys = group.All(dict =>
                dict != null &&
                dict.ContainsKey("Position") &&
                dict.ContainsKey("Direction"));

            if (bothHaveKeys)
                return true;
        }

        return false;
    }
}
