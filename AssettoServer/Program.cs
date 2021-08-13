using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Serilog;

namespace AssettoServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Prevent parsing errors in floats because some cultures use "," instead of "." as decimal separator
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File($"logs/{DateTime.Now:MMddyyyy_HHmmss}.txt")
                .CreateLogger();

            var config = new ACServerConfiguration().FromFiles();
            ACServer server = new ACServer(config);

            await server.StartAsync();
            await Task.Delay(-1);
        }
    }
}
