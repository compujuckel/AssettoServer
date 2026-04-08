using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace SamplePlugin;

public class SampleModule : AssettoServerModule<SampleConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<Sample>().AsSelf().As<IHostedService>().SingleInstance();
    }
}
