using System;
using System.Threading.Tasks;

namespace AssettoServer.Server.UserGroup;

public interface IUserGroup
{
    public Task<bool> ContainsAsync(ulong guid);
    public Task<bool> AddAsync(ulong guid);

    public event EventHandler<IUserGroup, EventArgs> Changed;
}
