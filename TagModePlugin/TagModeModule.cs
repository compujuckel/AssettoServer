using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace TagModePlugin;

public class TagModeModule : AssettoServerModule<TagModeConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<TagModePlugin>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterType<EntryCarTagMode>().AsSelf();
        builder.RegisterType<TagSession>().AsSelf();
    }
}
