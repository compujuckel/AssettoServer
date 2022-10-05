using FluentValidation;
using JetBrains.Annotations;

namespace GeoIPPlugin;

[UsedImplicitly]
public class GeoIPConfigurationValidator : AbstractValidator<GeoIPConfiguration>
{
    public GeoIPConfigurationValidator()
    {
        RuleFor(cfg => cfg.DatabasePath).NotEmpty();
    }
}
