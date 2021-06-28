using Qmmands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Commands.Attributes
{
    public class RequireAdminAttribute : CheckAttribute
    {
        public override ValueTask<CheckResult> CheckAsync(CommandContext context)
        {
            if(context is ACCommandContext acContext)
            {
                if (acContext.IsConsole || acContext.Client?.IsAdministrator == true)
                    return CheckResult.Successful;
                else
                    return CheckResult.Failed("You are not an administrator.");
            }

            return CheckResult.Failed("Invalid command context.");
        }
    }
}
