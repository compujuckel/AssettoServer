using AssettoServer.Server.Plugin;
using Autofac;

namespace ReportPlugin;

public class ReportPluginModule : AssettoServerModule<ReportConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ReportPlugin>().AsSelf().SingleInstance().AutoActivate();
    }
}
