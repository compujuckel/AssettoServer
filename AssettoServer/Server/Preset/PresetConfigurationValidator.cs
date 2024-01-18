using FluentValidation;
using JetBrains.Annotations;

namespace AssettoServer.Server.Preset;

[UsedImplicitly]
public class PresetConfigurationValidator : AbstractValidator<PresetConfiguration>
{
    public PresetConfigurationValidator()
    {
    }
}
