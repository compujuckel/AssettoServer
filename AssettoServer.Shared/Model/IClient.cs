namespace AssettoServer.Shared.Model;

public interface IClient
{
    public string? Name { get; }
    public string? Team { get; }
    public string? NationCode { get; }
}
