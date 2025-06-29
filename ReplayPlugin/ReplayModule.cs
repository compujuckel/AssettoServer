using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace ReplayPlugin;

public class ReplayModule : AssettoServerModule<ReplayConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ReplayPlugin>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterType<ReplayManager>().AsSelf().SingleInstance();
        builder.RegisterType<EntryCarExtraDataManager>().AsSelf().SingleInstance();
        builder.RegisterType<ReplayMetadataProvider>().AsSelf().SingleInstance();
    }
}
