using FluentValidation;

namespace GeoIPPlugin;

public class GeoIPConfigurationValidator : AbstractValidator<GeoIPConfiguration>
{
    public GeoIPConfigurationValidator()
    {
        RuleFor(cfg => cfg.DatabasePath).NotEmpty();
    }
}
