using Microsoft.Extensions.DependencyInjection;

namespace AssettoServer.Server.Plugin;

public interface IConfigureServices
{
    public void ConfigureServices(IServiceCollection services);
}
