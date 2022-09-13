namespace AssettoServer.Server.UserGroup;

public interface IUserGroupProvider
{
    public IUserGroup? Resolve(string name);
}
