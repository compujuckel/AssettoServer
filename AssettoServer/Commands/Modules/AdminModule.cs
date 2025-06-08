using AssettoServer.Commands.Attributes;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Weather;
using Qmmands;
using System;
using System.Globalization;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather.Implementation;
using AssettoServer.Server.Whitelist;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Utils;
using AssettoServer.Shared.Weather;
using AssettoServer.Utils;
using JetBrains.Annotations;

namespace AssettoServer.Commands.Modules;

[RequireAdmin]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class AdminModule : ACModuleBase
{
    private readonly IWeatherImplementation _weatherImplementation;
    private readonly WeatherManager _weatherManager;
    private readonly ACServerConfiguration _configuration;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly IWhitelistService _whitelist;

    public AdminModule(IWeatherImplementation weatherImplementation,
        WeatherManager weatherManager,
        ACServerConfiguration configuration,
        SessionManager sessionManager,
        EntryCarManager entryCarManager,
        IWhitelistService whitelist)
    {
        _weatherImplementation = weatherImplementation;
        _weatherManager = weatherManager;
        _configuration = configuration;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
        _whitelist = whitelist;
    }

    [Command("kick", "kick_id")]
    public Task KickAsync(ACTcpClient player, [Remainder] string? reason = null)
    {
        if (player.SessionId == Client?.SessionId)
            Reply("You cannot kick yourself.");
        else if (player.IsAdministrator)
            Reply("You cannot kick an administrator");
        else
        {
            Reply($"Steam profile of {player.Name}: https://steamcommunity.com/profiles/{player.Guid}");
            return _entryCarManager.KickAsync(player, reason, Client);
        }

        return Task.CompletedTask;
    }

    [Command("ban", "ban_id")]
    public Task BanAsync(ACTcpClient player, [Remainder] string? reason = null)
    {
        if (player.SessionId == Client?.SessionId)
            Reply("You cannot ban yourself.");
        else if (player.IsAdministrator)
            Reply("You cannot ban an administrator.");
        else
        {
            Reply($"Steam profile of {player.Name}: https://steamcommunity.com/profiles/{player.Guid}");
            if (player.OwnerGuid.HasValue && player.Guid != player.OwnerGuid)
            {
                Reply($"{player.Name} is using Steam Family Sharing, banning game owner https://steamcommunity.com/profiles/{player.OwnerGuid}");
            }
            return _entryCarManager.BanAsync(player, reason, Client);
        }

        return Task.CompletedTask;
    }

    [Command("next_session", "ksns")]
    public void NextSessionAsync()
    {
        Reply(_sessionManager.NextSession()
            ? "OK. Moving to next session"
            : "Error. Couldn't move to next session. Player is connecting or server is shutting down");
    }

    [Command("restart_session", "ksrs")]
    public void RestartSessionAsync()
    {
        Reply(_sessionManager.RestartSession()
            ? "OK. Restarting session"
            : "Error. Couldn't restart session. Player is connecting");
    }
    
    [Command("pit")]
    public void TeleportToPits([Remainder] ACTcpClient player)
    {
        _sessionManager.SendCurrentSession(player);
        player.SendChatMessage("You have been teleported to the pits.");

        if (player.SessionId != Client?.SessionId)
            Reply($"{player.Name} has been teleported to the pits.");
    }

    [Command("settime")]
    public void SetTime(string time)
    {
        if (DateTime.TryParseExact(time, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
        {
            _weatherManager.SetTime((int)dateTime.TimeOfDay.TotalSeconds);
            Broadcast("Time has been set.");
        }
        else
        {
            Reply("Invalid time format. Usage: /settime 15:31");
        }
    }

    [Command("setweather")]
    public void SetWeather(int weatherId)
    {
        if (_weatherManager.SetWeatherConfiguration(weatherId))
        {
            Reply("Weather configuration has been set.");
        }
        else
        {
            Reply("There is no weather configuration with this id.");
        }
    }

    [Command("cspweather")]
    public void CspWeather()
    {
        Reply("Available weathers:");
        foreach (WeatherFxType weather in Enum.GetValues<WeatherFxType>())
        {
            Reply($" - {weather}");
        }
    }

    [Command("setcspweather")]
    public void SetCspWeather(string upcomingStr, int duration)
    {
        if (Enum.TryParse(upcomingStr, true, out WeatherFxType upcoming))
        {
            _weatherManager.SetCspWeather(upcoming, duration);
            Reply("Weather has been set.");
        }
        else
        {
            Reply($"No weather with name '{upcomingStr}', use /cspweather for a list of available weathers.");
        }
    }

    [Command("setrain")]
    public void SetRain(float intensity, float wetness, float water)
    {
        _weatherManager.CurrentWeather.RainIntensity = intensity;
        _weatherManager.CurrentWeather.RainWetness = wetness;
        _weatherManager.CurrentWeather.RainWater = water;
        _weatherManager.SendWeather();
        Reply("Rain has been set.");
    }

    [Command("setgrip")]
    public void SetGrip(float grip)
    {
        if (grip is < 0 or > 1)
        {
            Reply("Invalid input, please use a decimal between 0 and 1. Example: 0.95");
        }
        else
        {
            _configuration.Server.DynamicTrack.OverrideGrip = grip;
            Reply("Grip has been set.");
        }
    }

    [Command("distance"), RequireConnectedPlayer]
    public void GetDistance([Remainder] ACTcpClient player)
    {
        Reply(Vector3.Distance(Client!.EntryCar.Status.Position, player.EntryCar.Status.Position).ToString(CultureInfo.InvariantCulture));
    }

    [Command("forcelights")]
    public void ForceLights(string toggle, [Remainder] ACTcpClient player)
    {
        bool forceLights = toggle == "on";
        player.EntryCar.ForceLights = forceLights;

        Reply($"{player.Name}'s lights {(forceLights ? "will" : "will not")} be forced on.");
    }

    [Command("whois")]
    public void WhoIs(ACTcpClient player)
    {
        Reply($"IP: {((IPEndPoint?)player.TcpClient.Client.RemoteEndPoint)?.Redact(_configuration.Extra.RedactIpAddresses)}");
        Reply($"Profile: https://steamcommunity.com/profiles/{player.Guid}\nPing: {player.EntryCar.Ping}ms");
        Reply($"Position: {player.EntryCar.Status.Position}\nVelocity: {(int)(player.EntryCar.Status.Velocity.Length() * 3.6)}kmh");
        if (player.OwnerGuid.HasValue && player.Guid != player.OwnerGuid)
        {
            Reply($"Steam Family Sharing Owner: https://steamcommunity.com/profiles/{player.OwnerGuid}");
        }
    }

    // keep restrict for backwards compatibility
    [Command("restrict", "restrictor")]
    public void Restrict(ACTcpClient player, int restrictor)
    {
        if (restrictor is > 400 or < 0)
        {
            Reply("SYNTAX ERROR: Use 'restrictor [driver numeric id] [0-400]'");
            return;
        }
        
        player.EntryCar.Restrictor = restrictor;
        player.SendPacket(new BallastUpdate { SessionId = player.SessionId, BallastKg = player.EntryCar.Ballast, Restrictor = player.EntryCar.Restrictor });
        Reply("Restrictor set.");
    }
        
    [Command("ballast")]
    public void Ballast(ACTcpClient? player = null, float? ballastKg = null)
    {
        if (player == null || ballastKg == null)
        {
            // Do not change the reply, it is used by CSP admin detection
            Reply("SYNTAX ERROR: Use 'ballast [driver numeric id] [kg]'");
            return;
        }
        if (ballastKg < 0)
        {
            Reply("SYNTAX ERROR: Use 'ballast [driver numeric id] [>=0 kg]'");
            return;
        }
        
        player.EntryCar.Ballast = ballastKg.Value;
        player.SendPacket(new BallastUpdate { SessionId = player.SessionId, BallastKg = player.EntryCar.Ballast, Restrictor = player.EntryCar.Restrictor });
        Reply("Ballast set.");
    }

    [Command("set")]
    public void Set(string key, [Remainder] string value)
    {
        try
        {
            Reply(_configuration.SetProperty(key, value) ? $"Property {key} set to {value}" : $"Could not set property {key}");
        }
        catch (Exception ex)
        {
            Reply(ex.Message);
        }
    }

    [Command("whitelist")]
    public async Task Whitelist(ulong guid)
    {
        await _whitelist.AddAsync(guid);
        Reply($"SteamID {guid} was added to the whitelist");
    }
    
    [Command("say")]
    public void Say([Remainder] string message)
    {
        Broadcast("CONSOLE: " + message);
    }

    [Command("noclip"), RequireConnectedPlayer]
    public void NoClip(bool enable)
    {
        if (_configuration.CSPTrackOptions.MinimumCSPVersion is null or < CSPVersion.V0_2_8)
        {
            Reply("Noclip is disabled. Please set a minimum required CSP version of 0.2.8 (3424) or higher");
            return;
        }
        
        Client?.EntryCar.SetCollisions(!enable);
        Reply(enable ? "Noclip enabled" : "Noclip disabled");
    }
    
#if DEBUG
    [Command("exception")]
    public void ThrowException()
    {
        throw new Exception("test");
    }
#endif
}
