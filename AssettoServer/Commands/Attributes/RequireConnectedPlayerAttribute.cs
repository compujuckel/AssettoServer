using System.Threading.Tasks;
using AssettoServer.Commands.Contexts;
using Qmmands;

namespace AssettoServer.Commands.Attributes;

public class RequireConnectedPlayerAttribute : CheckAttribute
{
    public override ValueTask<CheckResult> CheckAsync(CommandContext context)
    {
        return context is ChatCommandContext ? CheckResult.Successful : CheckResult.Failed("This command cannot be executed via RCON.");
    }
}
