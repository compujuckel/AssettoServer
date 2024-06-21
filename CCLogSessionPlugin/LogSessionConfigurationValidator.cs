using FluentValidation;
using JetBrains.Annotations;

namespace LogSessionPlugin;

[UsedImplicitly]
public class LogSessionConfigurationValidator : AbstractValidator<CCLogSessionConfiguration>
{
    public LogSessionConfigurationValidator()
    {
        RuleFor(cfg => cfg.ApiUrlPlayerDisconnect).NotEmpty();
        RuleFor(cfg => cfg.ApiUrlSessionEnd).NotEmpty();
        RuleFor(cfg => cfg.CrtPath).NotNull().Unless(cfg => cfg.KeyPath is null);
        RuleFor(cfg => cfg.KeyPath).NotNull().Unless(cfg => cfg.CrtPath is null);
    }
}
