using System.Threading.Tasks;
using Qmmands;

namespace AssettoServer.Commands.Attributes;

public class RequireConnectedPlayerAttribute : CheckAttribute
{
    public override ValueTask<CheckResult> CheckAsync(CommandContext context)
    {
        if (context is ACCommandContext acContext && acContext.Client != null)
            return CheckResult.Successful;

        return CheckResult.Failed("This command cannot be executed via RCON.");
    }
}
