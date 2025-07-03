using FluentValidation;

namespace FastTravelPlugin;

public class FastTravelConfigurationValidator : AbstractValidator<FastTravelConfiguration>
{
    public FastTravelConfigurationValidator()
    {
        RuleFor(cfg => cfg.MapFixedTargetPosition)
            .Must(x => x.Count == 3)
            .WithMessage("Must contain exactly 3 numeric values");
        RuleFor(cfg => cfg.MapMoveSpeeds.Count)
            .Equal(cfg => cfg.MapZoomValues.Count)
            .WithMessage("MapMoveSpeeds and MapZoomValues must contain the same number of values");
        RuleFor(cfg => cfg.MapZoomValues)
            .Must(x =>x.SequenceEqual(x.Order().ToList()))
            .WithMessage("Values should not be lower than previous ones");
        RuleFor(cfg => cfg.MapMoveSpeeds)
            .Must(x => x[..^1].SequenceEqual(x[..^1].Order().ToList()))
            .WithMessage("Values should not be lower than previous ones");
        RuleFor(cfg => cfg.MapMoveSpeeds)
            .Must(x => x.Last() == 0)
            .WithMessage("Last Move Speed Value must be 0");
    }
}
