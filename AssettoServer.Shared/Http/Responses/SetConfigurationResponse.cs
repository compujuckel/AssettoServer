namespace AssettoServer.Shared.Http.Responses;

public class SetConfigurationResponse
{
    public required string Status { get; init; }
    public string ErrorMessage { get; init; } = "";
}
