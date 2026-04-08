using System;
using AssettoServer.Server;
using Qmmands;
using Serilog;

namespace AssettoServer.Commands.Contexts;

public abstract class BaseCommandContext(
        EntryCarManager entryCarManager,
        IServiceProvider? serviceProvider = null)
    : CommandContext(serviceProvider)
{
    public virtual bool IsAdministrator => false;
    
    public abstract void Reply(string message);

    public virtual void Broadcast(string message)
    {
        Log.Information("Broadcast: {Message}", message);
        entryCarManager.BroadcastChat(message);
    }
}
