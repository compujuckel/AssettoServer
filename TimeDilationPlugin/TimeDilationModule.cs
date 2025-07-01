using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace TimeDilationPlugin;

public class TimeDilationModule : AssettoServerModule<TimeDilationConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<TimeDilationPlugin>().AsSelf().As<IHostedService>().SingleInstance();
    }
}
