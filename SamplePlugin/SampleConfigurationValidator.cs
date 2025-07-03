using FluentValidation;

namespace SamplePlugin;

// Use FluentValidation to validate plugin configuration
public class SampleConfigurationValidator : AbstractValidator<SampleConfiguration>
{
    public SampleConfigurationValidator()
    {
        RuleFor(cfg => cfg.Hello).Matches("World");
    }
}
