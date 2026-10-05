using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace CustomCommandPlugin;

public class CustomCommandModule : AssettoServerModule<CustomCommandConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<CustomCommand>().AsSelf().As<IHostedService>().SingleInstance();
    }
}
