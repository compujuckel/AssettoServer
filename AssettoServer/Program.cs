using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using CommandLine;
using Serilog;

namespace AssettoServer
{
    class Program
    {
        public class Options
        {
            [Option('p', "preset", Required = false, HelpText = "Configuration preset directory")]
            public string PresetFolder { get; set; } = "cfg";
        }
        
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

            var options = Parser.Default.ParseArguments<Options>(args).Value;
            if (options == null) return;

            var version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
            version = version.Substring(version.IndexOf('+') + 1);
            
            Log.Information("AssettoServer {0}", version);
            Log.Information("Using preset {0}", options.PresetFolder);

            var config = new ACServerConfiguration().FromFiles(options.PresetFolder);
            config.ServerVersion = version;
            
            ACServer server = new ACServer(config);

            await server.StartAsync();
            await Task.Delay(-1);
        }
    }
}
