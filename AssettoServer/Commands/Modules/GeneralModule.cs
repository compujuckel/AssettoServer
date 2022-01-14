using AssettoServer.Server;
using Qmmands;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AssettoServer.Commands.Modules;

public class GeneralModule : ACModuleBase
{
    [Command("ping")]
    public void Ping()
        => Reply($"Pong! {Context.Client?.EntryCar.Ping ?? 0}ms.");

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

    [Command("admin")]
    public void AdminAsync(string password)
    {
        if (IsConsole)
            Reply("You are the console.");
        else if (Context.Client.IsAdministrator)
            Reply("You are already an administrator.");
        else if (password == Context.Server.Configuration.AdminPassword)
        {
            Context.Client.IsAdministrator = true;
            Reply("You have logged in as an administrator.");
        }
        else
            Reply("Incorrect administrator password.");
    }

    [Command("legal")]
    public async Task ShowLegalNotice()
    {
        using var sr = new StringReader(LegalNotice.LegalNoticeText);
        string line;
        while ((line = await sr.ReadLineAsync()) != null)
        {
            Reply(line);
        }
    }
}