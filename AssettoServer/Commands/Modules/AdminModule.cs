using AssettoServer.Commands.Attributes;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Weather;
using Qmmands;
using System;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AssettoServer.Commands.Modules;

[RequireAdmin]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class AdminModule : ACModuleBase
{
    [Command("kick", "kick_id")]
    public Task KickAsync(ACTcpClient player, [Remainder] string? reason = null)
    {
        if (player.SessionId == Context.Client.SessionId)
            Reply("You cannot kick yourself.");
        else if (player.IsAdministrator)
            Reply("You cannot kick an administrator");
        else
        {
            Reply($"Steam profile of {player.Name}: https://steamcommunity.com/profiles/{player.Guid}");
                
            string kickMessage = reason == null ? $"{player.Name} has been kicked." : $"{player.Name} has been kicked for: {reason}.";
            return Context.Server.KickAsync(player, KickReason.Kicked, kickMessage, true, Context.Client);
        }

        return Task.CompletedTask;
    }

    [Command("ban", "ban_id")]
    public Task BanAsync(ACTcpClient player, [Remainder] string? reason = null)
    {
        if (player.SessionId == Context.Client.SessionId)
            Reply("You cannot ban yourself.");
        else if (player.IsAdministrator)
            Reply("You cannot ban an administrator.");
        else
        {
            Reply($"Steam profile of {player.Name}: https://steamcommunity.com/profiles/{player.Guid}");
                
            string kickMessage = reason == null ? $"{player.Name} has been banned." : $"{player.Name} has been banned for: {reason}.";
            return Context.Server.BanAsync(player, KickReason.VoteBlacklisted, kickMessage, Context.Client);
        }

        return Task.CompletedTask;
    }

    [Command("pit")]
    public void TeleportToPits([Remainder] ACTcpClient player)
    {
        Context.Server.SendCurrentSession(player);
        player.SendPacket(new ChatMessage { SessionId = 255, Message = "You have been teleported to the pits." });

        if (player.SessionId != Context.Client.SessionId)
            Reply($"{player.Name} has been teleported to the pits.");
    }

    [Command("settime")]
    public void SetTime(float time)
    {
        Context.Server.SetTime(time);
        Broadcast("Time has been set.");
    }

    [Command("setweather")]
    public void SetWeather(int weatherId)
    {
        if (Context.Server.WeatherProvider.SetWeatherConfiguration(weatherId))
        {
            Reply("Weather configuration has been set.");
        }
        else
        {
            Reply("There is no weather configuration with this id.");
        }
    }

    [Command("setcspweather")]
    public void SetCspWeather(int upcoming, int duration)
    {
        Context.Server.SetCspWeather((WeatherFxType)upcoming, duration);
        Reply("Weather has been set.");
    }

    [Command("setrain")]
    public void SetRain(float intensity, float wetness, float water)
    {
        Context.Server.CurrentWeather.RainIntensity = intensity;
        Context.Server.CurrentWeather.RainWetness = wetness;
        Context.Server.CurrentWeather.RainWater = water;
        Context.Server.WeatherImplementation.SendWeather();
    }

    [Command("setgrip")]
    public void SetGrip(float grip)
    {
        Context.Server.CurrentWeather.TrackGrip = grip;
        Context.Server.WeatherImplementation.SendWeather();
    }

    [Command("distance")]
    public void GetDistance([Remainder] ACTcpClient player)
    {
        Reply(Vector3.Distance(Context.Client.EntryCar.Status.Position, player.EntryCar.Status.Position).ToString(CultureInfo.InvariantCulture));
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
        Reply($"IP: {(player.TcpClient.Client.RemoteEndPoint as System.Net.IPEndPoint)?.Address}\nProfile: https://steamcommunity.com/profiles/{player.Guid}\nPing: {player.EntryCar.Ping}ms");
        Reply($"Position: {player.EntryCar.Status.Position}\nVelocity: {(int)(player.EntryCar.Status.Velocity.Length() * 3.6)}kmh");
    }

    [Command("restrict")]
    public void Restrict(ACTcpClient player, float restrictor, float ballastKg)
    {
        player.SendPacket(new BallastUpdate { SessionId = player.SessionId, BallastKg = ballastKg, Restrictor = restrictor });
        Reply("Restrictor and ballast set.");
    }
        
    // Do not change the reply, it is used by CSP admin detection
    [Command("ballast")]
    public void Ballast()
    {
        Reply("SYNTAX ERROR: Use 'ballast [driver numeric id] [kg]'");
    }

    [Command("set")]
    public void Set(string key, [Remainder] string value)
    {
        try
        {
            Reply(Context.Server.Configuration.SetProperty(key, value) ? $"Property {key} set to {value}" : $"Could not set property {key}");
        }
        catch (Exception ex)
        {
            Reply(ex.Message);
        }
    }
}
