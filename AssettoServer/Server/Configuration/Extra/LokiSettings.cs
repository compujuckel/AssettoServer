using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration.Extra;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class LokiSettings
{
    public string? Url { get; init; }
    public string? Login { get; init; }
    public string? Password { get; init; }

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(Login) && !string.IsNullOrEmpty(Password);
    }
}
