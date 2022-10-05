using FluentValidation;

namespace AssettoServer.Server.Configuration;

public class ACServerConfigurationValidator : AbstractValidator<ACServerConfiguration>
{
    public ACServerConfigurationValidator()
    {
        RuleFor(cfg => cfg.Extra.ValidateDlcOwnership).NotNull();
        RuleFor(cfg => cfg.Extra.OutsideNetworkBubbleRefreshRateHz).LessThanOrEqualTo(cfg => cfg.Server.RefreshRateHz);
        RuleFor(cfg => cfg.Extra.ServerDescription).NotNull();
        RuleFor(cfg => cfg.Extra.RainTrackGripReductionPercent).InclusiveBetween(0, 0.5);
        RuleFor(cfg => cfg.Extra.EnablePlugins).NotNull();
        RuleFor(cfg => cfg.Extra.IgnoreConfigurationErrors).NotNull();
        RuleFor(cfg => cfg.Extra.UserGroups).NotNull();
        RuleFor(cfg => cfg.Extra.BlacklistUserGroup).NotEmpty();
        RuleFor(cfg => cfg.Extra.WhitelistUserGroup).NotEmpty();
        RuleFor(cfg => cfg.Extra.AdminUserGroup).NotEmpty();

        RuleFor(cfg => cfg.Extra.AiParams.MinSpawnDistancePoints).LessThanOrEqualTo(cfg => cfg.Extra.AiParams.MaxSpawnDistancePoints);
        RuleFor(cfg => cfg.Extra.AiParams.MinAiSafetyDistanceMeters).LessThanOrEqualTo(cfg => cfg.Extra.AiParams.MaxAiSafetyDistanceMeters);
        RuleFor(cfg => cfg.Extra.AiParams.MinSpawnProtectionTimeSeconds).LessThanOrEqualTo(cfg => cfg.Extra.AiParams.MaxSpawnProtectionTimeSeconds);
        RuleFor(cfg => cfg.Extra.AiParams.MinCollisionStopTimeSeconds).LessThanOrEqualTo(cfg => cfg.Extra.AiParams.MaxCollisionStopTimeSeconds);
        RuleFor(cfg => cfg.Extra.AiParams.MaxSpeedVariationPercent).InclusiveBetween(0, 1);
        RuleFor(cfg => cfg.Extra.AiParams.DefaultAcceleration).GreaterThan(0);
        RuleFor(cfg => cfg.Extra.AiParams.DefaultDeceleration).GreaterThan(0);
        RuleFor(cfg => cfg.Extra.AiParams.NamePrefix).NotNull();
        RuleFor(cfg => cfg.Extra.AiParams.IgnoreObstaclesAfterSeconds).GreaterThanOrEqualTo(0);
        // TODO hourly traffic density
        RuleFor(cfg => cfg.Extra.AiParams.CarSpecificOverrides).NotNull();
        RuleForEach(cfg => cfg.Extra.AiParams.CarSpecificOverrides).ChildRules(overrides =>
        {
            overrides.RuleFor(o => o.SkinSpecificOverrides).NotNull();
        });

        RuleFor(cfg => cfg.Server).ChildRules(server =>
        {
            server.RuleFor(s => s.AdminPassword).NotEmpty().MinimumLength(8);
            server.RuleFor(s => s.Track).NotEmpty();
            server.RuleFor(s => s.TrackConfig).NotNull();
            server.RuleFor(s => s.FuelConsumptionRate).GreaterThanOrEqualTo(0);
            server.RuleFor(s => s.MechanicalDamageRate).GreaterThanOrEqualTo(0);
            server.RuleFor(s => s.TyreConsumptionRate).GreaterThanOrEqualTo(0);
            server.RuleFor(s => s.LegalTyres).NotNull();
            server.RuleFor(s => s.WelcomeMessagePath).NotNull();
            server.RuleFor(s => s.TimeOfDayMultiplier).GreaterThanOrEqualTo(0);

            server.RuleForEach(s => s.Weathers).ChildRules(weather =>
            {
                weather.RuleFor(w => w.BaseTemperatureAmbient).GreaterThanOrEqualTo(0);
                weather.RuleFor(w => w.BaseTemperatureRoad).GreaterThanOrEqualTo(0);
                weather.RuleFor(w => w.WindBaseSpeedMin).LessThanOrEqualTo(w => w.WindBaseSpeedMax);
            });

            server.RuleFor(s => s.DynamicTrack).NotNull().ChildRules(dynTrack =>
            {
                dynTrack.RuleFor(d => d.BaseGrip).InclusiveBetween(0, 1);
            });
            
            // TODO session configs
        });
    }
}
