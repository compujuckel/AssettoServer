using System.Threading.Tasks;
using AssettoServer.Shared.Network.Http.Responses;

namespace AssettoServer.Server.CMContentProviders;

public interface ICMContentProvider
{
    public ValueTask<CMContentConfiguration?> GetContentAsync(ulong guid);
}
