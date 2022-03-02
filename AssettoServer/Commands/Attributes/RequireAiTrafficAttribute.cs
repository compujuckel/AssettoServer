using System.Threading.Tasks;
using Qmmands;

namespace AssettoServer.Commands.Attributes;

public class RequireAiTrafficAttribute : CheckAttribute
{
    public override ValueTask<CheckResult> CheckAsync(CommandContext context)
    {
        if (context is ACCommandContext acContext)
        {
            return acContext.Server.AiEnabled ? CheckResult.Successful : CheckResult.Failed("AI not enabled");
        }
        
        return CheckResult.Failed("Invalid command context.");
    }
}
