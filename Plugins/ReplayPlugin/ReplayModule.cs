using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace ReplayPlugin;

public class ReplayModule : AssettoServerModule<ReplayConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ReplayPlugin>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterType<ReplayService>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterType<ReplayWriter>().AsSelf().SingleInstance();
        builder.RegisterType<ReplaySegmentManager>().AsSelf().SingleInstance();
        builder.RegisterType<EntryCarExtraDataManager>().AsSelf().SingleInstance();
        builder.RegisterType<ReplayMetadataProvider>().AsSelf().SingleInstance();
    }
}
