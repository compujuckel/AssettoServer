using JetBrains.Annotations;
using Qmmands;

namespace AssettoServer.Commands;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public class ACModuleBase : ModuleBase<ACCommandContext>
{
    public void Reply(string message)
        => Context.Reply(message);

    public void Broadcast(string message)
        => Context.Broadcast(message);
}
