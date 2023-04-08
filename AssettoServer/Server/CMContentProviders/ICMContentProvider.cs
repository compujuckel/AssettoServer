using System.Threading.Tasks;
using AssettoServer.Shared.Http.Responses;

namespace AssettoServer.Server.CMContentProviders;

public interface ICMContentProvider
{
    public ValueTask<CMContentConfiguration?> GetContentAsync(ulong guid);
}
