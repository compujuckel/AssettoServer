using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using Qmmands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Commands.Modules
{
    public class GeneralModule : ACModuleBase
    {
        [Command("ping")]
        public void Ping()
            => Reply($"Pong! {Context.Client?.EntryCar.Ping ?? 0}ms.");

        [Command("time")]
        public void Time()
            => Reply($"It is currently {new DateTime().AddSeconds(Context.Server.CurrentDaySeconds):H:mm}.");

#if DEBUG
        [Command("test")]
        public ValueTask Test()
        {
            throw new Exception("Test exception");
        }
#endif

        [Command("race")]
        public void Race(ACTcpClient player)
            => Context.Client.EntryCar.ChallengeCar(player.EntryCar);

        [Command("accept")]
        public async ValueTask AcceptRaceAsync()
        {
            Race currentRace = Context.Client.EntryCar.CurrentRace;
            if (currentRace == null)
                Reply("You do not have a pending race request.");
            else if (currentRace.HasStarted)
                Reply("This race has already started.");
            else
                await currentRace.StartAsync();
        }

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
    }
}
