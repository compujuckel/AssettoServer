using Qmmands;
using System.Threading.Tasks;
using AssettoServer.Commands.Contexts;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.UserGroup;
using Microsoft.Extensions.DependencyInjection;

namespace AssettoServer.Commands.Attributes;

public class RequireExplicitAssignmentAttribute : CheckAttribute
{
    public override async ValueTask<CheckResult> CheckAsync(CommandContext context)
    {
        switch (context)
        {
            case ChatCommandContext chatContext:
            {
                var config = chatContext.Services.GetRequiredService<ACServerConfiguration>();
                var userGroupManager = chatContext.Services.GetRequiredService<UserGroupManager>();

                if (config.Extra.UserGroupCommandPermissions != null)
                {
                    foreach (var perm in config.Extra.UserGroupCommandPermissions)
                    {
                        if (perm.Commands.Contains(chatContext.Command.Name)
                            && userGroupManager.TryResolve(perm.UserGroup, out var group)
                            && await group.ContainsAsync(chatContext.Client.Guid))
                        {
                            return CheckResult.Successful;
                        }
                    }
                }
                goto default;
            }
            default:
                return CheckResult.Failed("You are not an administrator.");
        }
    }
}
