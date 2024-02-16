﻿using AssettoServer.Server;
using Qmmands;
using System;
using System.IO;
using System.Threading.Tasks;
using AssettoServer.Commands.Attributes;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using AssettoServer.Utils;
using JetBrains.Annotations;

namespace AssettoServer.Commands.Modules;

[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class GeneralModule : ACModuleBase
{
    private readonly WeatherManager _weatherManager;
    private readonly ACServerConfiguration _configuration;

    public GeneralModule(WeatherManager weatherManager, ACServerConfiguration configuration)
    {
        _weatherManager = weatherManager;
        _configuration = configuration;
    }

    [Command("ping"), RequireConnectedPlayer]
    public void Ping()
        => Reply($"Pong! {Client!.EntryCar.Ping}ms.");

    [Command("time")]
    public void Time()
        => Reply($"It is currently {_weatherManager.CurrentDateTime:H:mm}.");

#if DEBUG
    [Command("test")]
    public ValueTask Test()
    {
        throw new Exception("Test exception");
    }
#endif

    // Do not change the reply, it is used by CSP admin detection
    [Command("admin"), RequireConnectedPlayer]
    public void AdminAsync(string password)
    {
        if (_configuration.Server.CheckAdminPassword(password))
        {
            Client!.IsAdministrator = true;
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
    
    [Command("resetcar"), RequireConnectedPlayer]
    public void ResetCarAsync()
    {
        if (_configuration.Extra is { EnableCarReset: true, MinimumCSPVersion: >= CSPVersion.V0_2_3_p47 })
        {
            if (!Client!.EntryCar.TryResetPosition())
                Reply("Couldn't reset position");
        }
        else
            Reply("Reset is not enabled on this server");
                
    }
}
