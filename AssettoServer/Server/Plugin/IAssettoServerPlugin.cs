using JetBrains.Annotations;

namespace AssettoServer.Server.Plugin;

public interface IAssettoServerPlugin
{
    public void Initialize(ACServer server);
}

public interface IAssettoServerPlugin<T> : IAssettoServerPlugin
{
    [UsedImplicitly] public void SetConfiguration(T configuration);
}