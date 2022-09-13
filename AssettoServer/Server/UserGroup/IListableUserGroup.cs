using System.Collections.Generic;

namespace AssettoServer.Server.UserGroup;

public interface IListableUserGroup : IUserGroup
{
    public IReadOnlyCollection<ulong> List { get; }
}
