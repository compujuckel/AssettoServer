using Qmmands;
using System.Threading.Tasks;

namespace AssettoServer.Commands.Attributes
{
    public class RequireAdminAttribute : CheckAttribute
    {
        public override ValueTask<CheckResult> CheckAsync(CommandContext context)
        {
            if (context is ACCommandContext acContext)
            {
                return acContext.Client?.IsAdministrator == true ? CheckResult.Successful : CheckResult.Failed("You are not an administrator.");
            }

            return CheckResult.Failed("Invalid command context.");
        }
    }
}
