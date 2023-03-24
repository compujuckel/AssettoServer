using System.Threading.Tasks;
using AssettoServer.Server.Configuration;

namespace AssettoServer.Server.CMContentProviders;

public interface ICMContentProvider
{
    public ValueTask<CMContentConfiguration?> GetContentAsync(ulong guid);
}
