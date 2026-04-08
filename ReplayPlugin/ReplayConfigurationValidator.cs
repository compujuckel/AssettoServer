using FluentValidation;

namespace ReplayPlugin;

public class ReplayConfigurationValidator : AbstractValidator<ReplayConfiguration>
{
    public ReplayConfigurationValidator()
    {
        RuleFor(cfg => cfg.MinSegmentSizeKilobytes).GreaterThanOrEqualTo(100);
        RuleFor(cfg => cfg.MaxSegmentSizeKilobytes).GreaterThanOrEqualTo(cfg => cfg.MinSegmentSizeKilobytes);
        RuleFor(cfg => cfg.SegmentTargetSeconds).GreaterThanOrEqualTo(10);
        RuleFor(cfg => cfg.ReplayDurationSeconds).GreaterThan(30);
        RuleFor(cfg => cfg.RefreshRateDivisor).GreaterThanOrEqualTo(1);
    }
}
