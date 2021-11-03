using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CommandLine;
using Serilog;
using Serilog.Events;

namespace AssettoServer
{
    class Program
    {
        public class Options
        {
            [Option('p', "preset", Required = false, HelpText = "Configuration preset")]
            public string Preset { get; set; } = "";
        }
        
        static async Task Main(string[] args)
        {
            // Prevent parsing errors in floats because some cultures use "," instead of "." as decimal separator
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            
            var options = Parser.Default.ParseArguments<Options>(args).Value;
            if (options == null) return;

            string logPrefix = string.IsNullOrEmpty(options.Preset) ? "" : $"{options.Preset}-";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information) // TODO set to warning again
                .WriteTo.Console()
                .WriteTo.File($"logs/{logPrefix}{DateTime.Now:MMddyyyy_HHmmss}.txt")
                .CreateLogger();

            var version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
            version = version.Substring(version.IndexOf('+') + 1);
            
            Log.Information("AssettoServer {0}", version);
            Log.Information("Using preset {0}", options.Preset);

            string configDir = string.IsNullOrEmpty(options.Preset) ? "cfg" : Path.Join("presets", options.Preset);

            var config = new ACServerConfiguration().FromFiles(configDir);
            config.ServerVersion = version;
            
            ACServer server = new ACServer(config);

            await server.StartAsync();
            await Task.Delay(-1);
        }
    }
}
