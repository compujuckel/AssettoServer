using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using AssettoServer.Shared.Network.Http.Responses;

namespace AssettoServer.Server.CMContentProviders;

public interface ICMContentProvider
{
    public ValueTask<CMContentConfiguration?> GetContentAsync(ulong guid);

    public void Initialize();

    public bool TryGetZipPath(string type, string entry, [NotNullWhen(true)] out string? path);
}
