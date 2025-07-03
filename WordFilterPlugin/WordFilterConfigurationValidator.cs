using FluentValidation;

namespace WordFilterPlugin;

public class WordFilterConfigurationValidator : AbstractValidator<WordFilterConfiguration>
{
    public WordFilterConfigurationValidator()
    {
        RuleFor(cfg => cfg.ProhibitedUsernamePatterns).NotNull();
        RuleFor(cfg => cfg.ProhibitedChatPatterns).NotNull();
        RuleFor(cfg => cfg.BannableChatPatterns).NotNull();
    }
}
