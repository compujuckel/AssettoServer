using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using System;
using System.Globalization;
using System.Threading.Tasks;
using AssettoServer.Server.Plugin;
using CommandLine;
using Serilog;
using Serilog.Events;

namespace AssettoServer
{
    internal static class Program
    {
        private class Options
        {
            [Option('p', "preset", Required = false, SetName = "AssettoServer", HelpText = "Configuration preset")]
            public string Preset { get; set; } = "";

            [Option('c', Required = false, SetName = "Content Manager compatibility", HelpText = "Path to server configuration")]
            public string ServerCfgPath { get; set; } = "";

            [Option('e', Required = false, SetName = "Content Manager compatibility", HelpText = "Path to entry list")]
            public string EntryListPath { get; set; } = "";
        }
        
        static async Task Main(string[] args)
        {
            // Prevent parsing errors in floats because some cultures use "," instead of "." as decimal separator
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            
            var options = Parser.Default.ParseArguments<Options>(args).Value;
            if (options == null) return;

            string logPrefix = string.IsNullOrEmpty(options.Preset) ? "log" : options.Preset;

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .WriteTo.Console()
                .WriteTo.File($"logs/{logPrefix}-.txt",
                    rollingInterval: RollingInterval.Day)
                .CreateLogger();

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            Log.Information("AssettoServer {Version}", ThisAssembly.AssemblyInformationalVersion);
            if (!string.IsNullOrEmpty(options.Preset))
            {
                Log.Information("Using preset {Preset}", options.Preset);
            }

            ACPluginLoader loader = new ACPluginLoader();

            var config = new ACServerConfiguration().FromFiles(options.Preset, options.ServerCfgPath, options.EntryListPath, loader);
            config.ServerVersion = ThisAssembly.AssemblyInformationalVersion;
            
            ACServer server = new ACServer(config, loader);

            await server.StartAsync();
            await Task.Delay(-1);
        }

        static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Log.Fatal((Exception)args.ExceptionObject, "Unhandled exception occurred");
            Environment.Exit(1);
        }
    }
}
