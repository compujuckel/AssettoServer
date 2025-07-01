using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace ReportPlugin;

public class ReportPluginModule : AssettoServerModule<ReportConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ReportPlugin>().AsSelf().As<IHostedService>().SingleInstance();
    }
}
