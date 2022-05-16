using AssettoServer.Server.Plugin;
using Autofac;

namespace WordFilterPlugin;

public class WordFilterModule : AssettoServerModule<WordFilterConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<WordFilter>().AsSelf().SingleInstance().AutoActivate();
    }
}
