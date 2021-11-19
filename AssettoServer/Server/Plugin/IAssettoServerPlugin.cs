namespace AssettoServer.Server.Plugin;

public interface IAssettoServerPlugin
{
    public void Initialize(ACServer server);
}

public interface IAssettoServerPlugin<T> : IAssettoServerPlugin
{
    public void SetConfiguration(T configuration);
}