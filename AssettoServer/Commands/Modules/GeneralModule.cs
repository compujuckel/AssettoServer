using AssettoServer.Server;
using Qmmands;
using System;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AssettoServer.Commands.Modules;

[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class GeneralModule : ACModuleBase
{
    [Command("ping")]
    public void Ping()
        => Reply($"Pong! {Context.Client.EntryCar.Ping}ms.");

    [Command("time")]
    public void Time()
        => Reply($"It is currently {TimeZoneInfo.ConvertTimeFromUtc(Context.Server.CurrentDateTime, Context.Server.TimeZone):H:mm}.");

#if DEBUG
    [Command("test")]
    public ValueTask Test()
    {
        throw new Exception("Test exception");
    }
#endif

    // Do not change the reply, it is used by CSP admin detection
    [Command("admin")]
    public void AdminAsync(string password)
    {
        if (password == Context.Server.Configuration.AdminPassword)
        {
            Context.Client.IsAdministrator = true;
            Reply("You are now Admin for this server");
        }
        else
            Reply("Command refused");
    }

    [Command("legal")]
    public async Task ShowLegalNotice()
    {
        using var sr = new StringReader(LegalNotice.LegalNoticeText);
        string? line;
        while ((line = await sr.ReadLineAsync()) != null)
        {
            Reply(line);
        }
    }
}