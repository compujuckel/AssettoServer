using AssettoServer.Server.Configuration;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Network.Http;
using AssettoServer.Utils;
using Autofac.Extensions.DependencyInjection;
using CommandLine;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Prometheus;
using Serilog;
using Parser = CommandLine.Parser;

namespace AssettoServer;

internal static class Program
{
#if DEBUG
    public static readonly bool IsDebugBuild = true;
#else
    public static readonly bool IsDebugBuild = false;
#endif
    
    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
    private class Options
    {
        [Option('p', "preset", Required = false, SetName = "AssettoServer", HelpText = "Configuration preset")]
        public string Preset { get; set; } = "";

        [Option('c', Required = false, SetName = "Content Manager compatibility", HelpText = "Path to server configuration")]
        public string ServerCfgPath { get; set; } = "";

        [Option('e', Required = false, SetName = "Content Manager compatibility", HelpText = "Path to entry list")]
        public string EntryListPath { get; set; } = "";

        [Option("plugins-from-workdir", Required = false, HelpText = "Additionally load plugins from working directory")]
        public bool LoadPluginsFromWorkdir { get; set; } = false;
    }

    public static bool IsContentManager;
        
    internal static async Task Main(string[] args)
    {
        SetupFluentValidation();
        SetupMetrics();
        DetectContentManager();
        
        var options = Parser.Default.ParseArguments<Options>(args).Value;
        if (options == null) return;

        if (IsContentManager)
        {
            Console.OutputEncoding = Encoding.UTF8;
        }

        string logPrefix = string.IsNullOrEmpty(options.Preset) ? "log" : options.Preset;
        Logging.CreateDefaultLogger(logPrefix, IsContentManager);
        
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        Log.Information("AssettoServer {Version}", ThisAssembly.AssemblyInformationalVersion);
        if (IsContentManager)
        {
            Log.Debug("Server was started through Content Manager");
        }
        await RunServerAsync(options.Preset, options.ServerCfgPath, options.EntryListPath, options.LoadPluginsFromWorkdir);
    }

    private static async Task RunServerAsync(
        string preset,
        string serverCfgPath,
        string entryListPath,
        bool loadPluginsFromWorkdir = false,
        CancellationToken token = default)
    {
        var configLocations = ConfigurationLocations.FromOptions(preset, serverCfgPath, entryListPath);
        
        try
        {
            var config = new ACServerConfiguration(preset, configLocations, loadPluginsFromWorkdir);

            if (config.Extra.LokiSettings != null
                && !string.IsNullOrEmpty(config.Extra.LokiSettings.Url)
                && !string.IsNullOrEmpty(config.Extra.LokiSettings.Login)
                && !string.IsNullOrEmpty(config.Extra.LokiSettings.Password))
            {
                string logPrefix = string.IsNullOrEmpty(preset) ? "log" : preset;
                Logging.CreateLokiLogger(logPrefix, IsContentManager, preset, config.Extra.LokiSettings);
            }
            
            if (!string.IsNullOrEmpty(preset))
            {
                Log.Information("Using preset {Preset}", preset);
            }

            var host = Host.CreateDefaultBuilder()
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .UseSerilog()
                .ConfigureAppConfiguration(builder => { builder.Sources.Clear(); })
                .ConfigureWebHostDefaults(webHostBuilder =>
                {
                    webHostBuilder.ConfigureKestrel(serverOptions => serverOptions.AllowSynchronousIO = true)
                        .UseStartup(_ => new Startup(config))
                        .UseUrls($"http://0.0.0.0:{config.Server.HttpPort}");
                })
                .Build();
            
            await host.RunAsync(token);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Error starting server");
            string? crashReportPath = null;
            try
            {
                crashReportPath = CrashReportHelper.GenerateCrashReport(configLocations, ex);
            }
            catch (Exception ex2)
            {
                Log.Error(ex2, "Error writing crash report");
            }
            await Log.CloseAndFlushAsync();
            ExceptionHelper.PrintExceptionHelp(ex, IsContentManager, crashReportPath);
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        Log.Fatal((Exception)args.ExceptionObject, "Unhandled exception occurred");
        Log.CloseAndFlush();
        Environment.Exit(1);
    }

    private static void SetupFluentValidation()
    {
        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) =>
        {
            foreach (var attr in member!.GetCustomAttributes(true))
            {
                if (attr is IniFieldAttribute iniAttr)
                {
                    return iniAttr.Key;
                }
            }
            return member.Name;
        };
    }

    private static void SetupMetrics()
    {
        Metrics.ConfigureMeterAdapter(adapterOptions =>
        {
            // Disable a bunch of verbose / unnecessary default metrics
            adapterOptions.InstrumentFilterPredicate = inst => 
                inst.Name != "kestrel.active_connections" 
                && inst.Name != "http.server.active_requests"
                && inst.Name != "kestrel.queued_connections"
                && inst.Name != "http.server.request.duration"
                && inst.Name != "kestrel.connection.duration"
                && inst.Name != "aspnetcore.routing.match_attempts"
                && inst.Name != "dns.lookups.duration"
                && !inst.Name.StartsWith("http.client.");
        });
    }

    private static void DetectContentManager()
    {
        try
        {
            var parentId = Process.GetCurrentProcess().GetParentProcessId();
            IsContentManager = Process.GetProcessById(parentId).ProcessName == "Content Manager";
        }
        catch (Exception)
        {
            // ignored
        }
    }
}
