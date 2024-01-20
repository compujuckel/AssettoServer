using FluentValidation;
using JetBrains.Annotations;

namespace CyclePresetPlugin.Preset;

[UsedImplicitly]
public class PresetConfigurationValidator : AbstractValidator<PresetConfiguration>
{
    public PresetConfigurationValidator()
    {
        RuleFor(cfg => cfg.RandomTrack).ChildRules(randomTrack =>
        {
            randomTrack.RuleFor(c => c!.Weight).GreaterThanOrEqualTo(0f);
        });
        
    }
}
