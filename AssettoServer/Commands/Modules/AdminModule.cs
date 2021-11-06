using AssettoServer.Commands.Attributes;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using AssettoServer.Network.Udp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using Humanizer;
using Humanizer.Bytes;
using Qmmands;
using System;
using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;

namespace AssettoServer.Commands.Modules
{
    [RequireAdmin]
    public class AdminModule : ACModuleBase
    {
        [Command("kick", "kick_id")]
        public Task KickAsync(ACTcpClient player, [Remainder] string reason = null)
        {
            if (player.SessionId == Context.Client?.SessionId)
                Reply("You cannot kick yourself.");
            if (player.IsAdministrator)
                Reply("You cannot kick an administrator");
            else
            {
                string kickMessage = reason == null ? $"{player.Name} ({player.Guid}) has been kicked." : $"{player.Name} has been kicked for: {reason}.";
                return Context.Server.KickAsync(player, KickReason.None, kickMessage, true, Context.Client);
            }

            return Task.CompletedTask;
        }

        [Command("ban", "ban_id")]
        public ValueTask BanAsync(ACTcpClient player, [Remainder] string reason = null)
        {
            if (player.SessionId == Context.Client?.SessionId)
                Reply("You cannot ban yourself.");
            else if (player.IsAdministrator)
                Reply("You cannot ban an administrator.");
            else
            {
                string kickMessage = reason == null ? $"{player.Name} has been banned." : $"{player.Name} ({player.Guid}) has been banned for: {reason}.";
                return Context.Server.BanAsync(player, KickReason.Blacklisted, kickMessage, Context.Client);
            }

            return ValueTask.CompletedTask;
        }

        [Command("unban")]
        public async Task UnbanAsync(string guid)
        {
            if (Context.Server.Blacklist.ContainsKey(guid))
            {
                await Context.Server.UnbanAsync(guid);
                Reply($"{guid} has been unbanned.");
            }
            else Reply($"ID {guid} is not banned.");
        }

        [Command("pit")]
        public void TeleportToPits([Remainder] ACTcpClient player)
        {
            EntryCar car = player.EntryCar;

            car.Client.SendCurrentSession();
            car.Client.SendPacket(new ChatMessage { SessionId = 255, Message = "You have been teleported to the pits." });

            if (player.SessionId != Context.Client.SessionId)
                Reply($"{car.Client.Name} has been teleported to the pits.");
        }

        [Command("settime")]
        public void SetTime(float time)
        {
            Context.Server.SetTime(time);
            Broadcast("Time has been set.");
        }

        [Command("settimemult")]
        public void SetTimeMult(float multiplier)
        {
            Context.Server.Configuration.TimeOfDayMultiplier = multiplier;
        }

        [Command("setweather")]
        public void SetWeather(int weatherId)
        {
            if (Context.Server.WeatherProvider is DefaultWeatherProvider provider)
            {
                if (provider.SetWeatherConfiguration(weatherId))
                {
                    Reply("Weather has been set.");
                }
                else
                {
                    Reply("There is no weather with this id.");
                }
            }
            else
            {
                Reply("Setting a weather configuration is not supported.");
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
            Context.Server.SendCurrentWeather();
        }

        [Command("setgrip")]
        public void SetGrip(float grip)
        {
            Context.Server.CurrentWeather.TrackGrip = grip;
            Context.Server.SendCurrentWeather();
        }

        [Command("setafktime")]
        public void SetAfkTime(int time)
        {
            time = Math.Max(1, time);
            Context.Server.Configuration.Extra.MaxAfkTimeMinutes = time;

            Reply($"Maximum AFK time has been set to {time} minutes.");
        }

        [Command("forcelights")]
        public void ForceLights(string toggle)
        {
            bool forceLights = toggle == "on";
            Context.Server.Configuration.Extra.ForceLights = forceLights;

            Reply($"Lights {(forceLights ? "will" : "will not")} be forced on.");
        }

        [Command("distance")]
        public void GetDistance([Remainder] ACTcpClient player)
        {
            Reply(Vector3.Distance(Context.Client.EntryCar.Status.Position, player.EntryCar.Status.Position).ToString());
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
            EntryCar car = player.EntryCar;

            Reply($"IP: {(player.TcpClient.Client.RemoteEndPoint as System.Net.IPEndPoint).Address}\nProfile: https://steamcommunity.com/profiles/{player.Guid}\nPing: {car.Ping}ms");
            Reply($"Position: {car.Status.Position}\nVelocity: {(int)(car.Status.Velocity.Length() * 3.6)}kmh");
        }

        [Command("restrict")]
        public void Restrict(ACTcpClient player, float restrictor, float ballastKg)
        {
            player.SendPacket(new BallastUpdate { SessionId = player.SessionId, BallastKg = ballastKg, Restrictor = restrictor });
            Reply("Restrictor and ballast set.");
        }
        
        // CSP uses this to detect if the player is admin
        [Command("ballast")]
        public void Ballast()
        {
            Reply("SYNTAX ERROR: Use 'ballast [driver numeric id] [kg]'");
        }

        [Command("netstats")]
        public void NetStats()
        {
            ACUdpServer udpServer = Context.Server.UdpServer;
            Reply($"Sent: {udpServer.DatagramsSentPerSecond} packets/s ({ByteSize.FromBytes(udpServer.BytesSentPerSecond).Per(TimeSpan.FromSeconds(1)).Humanize("#.##")})\n" +
                $"Received: {udpServer.DatagramsReceivedPerSecond} packets/s ({ByteSize.FromBytes(udpServer.BytesReceivedPerSecond).Per(TimeSpan.FromSeconds(1)).Humanize("#.##")})");
        }

        [Command("chatlog")]
        public void ChatLog(bool enable)
        {
            Context.Client.IsChatLogEnabled = enable;
        }
    }
}
