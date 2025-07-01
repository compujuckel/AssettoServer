using AssettoServer.Commands.Contexts;
using AssettoServer.Network.Tcp;
using JetBrains.Annotations;
using Qmmands;

namespace AssettoServer.Commands;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public class ACModuleBase : ModuleBase<BaseCommandContext>
{
    public PlayerClient? Client => (Context as ChatCommandContext)?.Client;
    
    public void Reply(string message)
        => Context.Reply(message);

    public void Broadcast(string message)
        => Context.Broadcast(message);
}
