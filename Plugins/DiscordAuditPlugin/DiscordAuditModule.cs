using AssettoServer.Server.Plugin;
using Autofac;

namespace DiscordAuditPlugin;

public class DiscordAuditModule : AssettoServerModule<DiscordAuditConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<Discord>().AsSelf().AutoActivate().SingleInstance();
    }
}
