using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Http.Responses;

namespace AssettoServer.Server.CMContentProviders;

public class DefaultCMContentProvider : ICMContentProvider
{
    private readonly ACServerConfiguration _configuration;

    public DefaultCMContentProvider(ACServerConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ValueTask<CMContentConfiguration?> GetContentAsync(ulong guid)
    {
        return ValueTask.FromResult(_configuration.ContentConfiguration);
    }
}
