using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Network.Http.Responses;

namespace AssettoServer.Server.CMContentProviders;

public class DefaultCMContentProvider : ICMContentProvider
{
    private readonly CMContentConfiguration? _contentConfiguration;

    public DefaultCMContentProvider(ACServerConfiguration configuration)
    {
        _contentConfiguration = configuration.ContentConfiguration;
    }

    public ValueTask<CMContentConfiguration?> GetContentAsync(ulong guid)
    {
        return ValueTask.FromResult(_contentConfiguration);
    }
}
