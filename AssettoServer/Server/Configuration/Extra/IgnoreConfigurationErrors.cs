using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration.Extra;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class IgnoreConfigurationErrors
{
    public bool MissingCarChecksums { get; init; }
    public bool MissingTrackParams { get; init; }
    public bool UnsafeAdminWhitelist { get; init; }
}
