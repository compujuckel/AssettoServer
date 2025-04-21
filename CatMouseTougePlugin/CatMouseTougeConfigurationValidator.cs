using FluentValidation;
using JetBrains.Annotations;

namespace CatMouseTougePlugin;

// Use FluentValidation to validate plugin configuration
[UsedImplicitly]
public class CatMouseTougeConfigurationValidator : AbstractValidator<CatMouseTougeConfiguration>
{
    public CatMouseTougeConfigurationValidator()
    {
        RuleFor(cfg => cfg.Message).Matches("World");
    }
}
