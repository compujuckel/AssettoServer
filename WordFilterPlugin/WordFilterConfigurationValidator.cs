using FluentValidation;
using JetBrains.Annotations;

namespace WordFilterPlugin;

[UsedImplicitly]
public class WordFilterConfigurationValidator : AbstractValidator<WordFilterConfiguration>
{
    public WordFilterConfigurationValidator()
    {
        RuleFor(cfg => cfg.ProhibitedUsernamePatterns).NotNull();
        RuleFor(cfg => cfg.ProhibitedChatPatterns).NotNull();
        RuleFor(cfg => cfg.BannableChatPatterns).NotNull();
    }
}
