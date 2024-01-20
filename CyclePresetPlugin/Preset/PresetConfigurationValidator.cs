using FluentValidation;
using JetBrains.Annotations;

namespace CyclePresetPlugin.Preset;

[UsedImplicitly]
public class PresetConfigurationValidator : AbstractValidator<PresetConfiguration>
{
    public PresetConfigurationValidator()
    {
    }
}
