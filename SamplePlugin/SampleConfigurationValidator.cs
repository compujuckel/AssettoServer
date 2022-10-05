using FluentValidation;
using JetBrains.Annotations;

namespace SamplePlugin;

// Use FluentValidation to validate plugin configuration
[UsedImplicitly]
public class SampleConfigurationValidator : AbstractValidator<SampleConfiguration>
{
    public SampleConfigurationValidator()
    {
        RuleFor(cfg => cfg.Hello).Matches("World");
    }
}
