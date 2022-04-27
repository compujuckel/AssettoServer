using System.Threading.Tasks;

namespace AssettoServer.Server.Admin;

public interface IAdminService
{
    public Task<bool> IsAdminAsync(ulong guid);
}
