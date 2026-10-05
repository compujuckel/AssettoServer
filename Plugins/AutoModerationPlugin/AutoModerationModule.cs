using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace AutoModerationPlugin;

public class AutoModerationModule : AssettoServerModule<AutoModerationConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AutoModerationPlugin>().AsSelf().As<IHostedService>().SingleInstance();
        builder.RegisterType<EntryCarAutoModeration>().AsSelf();
    }
}
