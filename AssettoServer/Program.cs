using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Utils;
using Autofac.Extensions.DependencyInjection;
using CommandLine;
using DotNext.Collections.Generic;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;
using Serilog;
using Parser = CommandLine.Parser;

namespace AssettoServer;

public static class Program
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

        [Option("verbose", Required = false, HelpText = "Change log level to verbose")]
        public bool UseVerboseLogging { get; set; } = false;
        
        [Option('r',"use-random-preset", Required = false, HelpText = "Use a random available configuration preset")]
        public bool UseRandomPreset { get; set; } = false;
        
        [Option('g',"generate-config", Required = false, HelpText = "Generate configuration file for all installed plugins")]
        public bool GenerateConfigs { get; set; } = false;
    }

    private class StartOptions
    {
        public string? Preset { get; init; }
        public string? ServerCfgPath { get; init; }
        public string? EntryListPath { get; init; }
        public PortOverrides? PortOverrides { get; init; }
    }

    public static bool IsContentManager { get; private set; }
    public static ConfigurationLocations? ConfigurationLocations { get; private set; }
    
    private static bool _loadPluginsFromWorkdir;
    private static bool _generatePluginConfigs;
    private static TaskCompletionSource<StartOptions> _restartTask = new();
    
    internal static async Task Main(string[] args)
    {
        SetupFluentValidation();
        SetupMetrics();
        DetectContentManager();
        
        var options = Parser.Default.ParseArguments<Options>(args).Value;
        if (options == null) return;

        _loadPluginsFromWorkdir = options.LoadPluginsFromWorkdir;
        _generatePluginConfigs = options.GenerateConfigs;
        
        if (IsContentManager)
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        
        if (options.UseRandomPreset)
        {
            var presetsPath = Path.Join(AppContext.BaseDirectory, "presets");
            var presets = Path.Exists(presetsPath) ? 
                Directory.EnumerateDirectories("presets").Select(Path.GetFileName).OfType<string>().ToArray() : [];
            
            if (presets.Length > 0)
                options.Preset = presets[Random.Shared.Next(presets.Length)];
            else 
                Log.Warning("Presets directory does not exist or contain any preset");
        }

        string logPrefix = string.IsNullOrEmpty(options.Preset) ? "log" : options.Preset;
        Logging.CreateLogger(logPrefix, IsContentManager, options.Preset, options.UseVerboseLogging);
        
        AppDomain.CurrentDomain.UnhandledException += UnhandledException;
        Log.Information("AssettoServer {Version}", ThisAssembly.AssemblyInformationalVersion);
        if (IsContentManager)
        {
            Log.Debug("Server was started through Content Manager");
        }

        var startOptions = new StartOptions
        {
            Preset = options.Preset,
            ServerCfgPath = options.ServerCfgPath,
            EntryListPath = options.EntryListPath
        };
        
        while (true)
        {
            _restartTask = new TaskCompletionSource<StartOptions>();
            using var cts = new CancellationTokenSource();
            var serverTask = RunServerAsync(startOptions.Preset, startOptions.ServerCfgPath, startOptions.EntryListPath, startOptions.PortOverrides ,options.UseVerboseLogging, cts.Token);
            var finishedTask = await Task.WhenAny(serverTask, _restartTask.Task);

            if (finishedTask == _restartTask.Task)
            {
                await cts.CancelAsync();
                await serverTask;

                startOptions = _restartTask.Task.Result;
            }
            else break;
        }
    }

    public static void RestartServer(
        string? preset,
        string? serverCfgPath = null,
        string? entryListPath = null,
        PortOverrides? portOverrides = null)
    {
        Log.Information("Initiated in-process server restart");
        _restartTask.SetResult(new StartOptions
        {
            Preset = preset,
            ServerCfgPath = serverCfgPath,
            EntryListPath = entryListPath,
            PortOverrides = portOverrides,
        });
    }

    private static async Task RunServerAsync(
        string? preset,
        string? serverCfgPath,
        string? entryListPath,
        PortOverrides? portOverrides,
        bool useVerboseLogging,
        CancellationToken token = default)
    {
        ConfigurationLocations = ConfigurationLocations.FromOptions(preset, serverCfgPath, entryListPath);
        
        try
        {
            var config = new ACServerConfiguration(preset, ConfigurationLocations, _loadPluginsFromWorkdir, _generatePluginConfigs, portOverrides);

            string logPrefix = string.IsNullOrEmpty(preset) ? "log" : preset;
            Logging.CreateLogger(logPrefix, IsContentManager, preset, useVerboseLogging, config.Extra.RedactIpAddresses, config.Extra.LokiSettings);

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
                    webHostBuilder.ConfigureKestrel(o => o.ConfigureEndpointDefaults(lo =>
                            lo.ApplicationServices
                                .GetServices<Func<ConnectionDelegate, ConnectionDelegate>>()
                                .ForEach(m => lo.Use(m))))
                        .UseStartup(_ => new Startup(config))
                        .UseUrls($"http://0.0.0.0:{config.Server.HttpPort}");
                })
                .Build();

            var applicationLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            var stoppedRegistration = applicationLifetime.ApplicationStopped
                .Register(() => OnApplicationStopped(applicationLifetime, host.Services.GetServices<IHostedService>()));

            await host.RunAsync(token);
            await stoppedRegistration.DisposeAsync();
        }
        catch (Exception ex)
        {
            CrashReportHelper.HandleFatalException(ex);
        }
    }
    
    // This handles all exceptions thrown in BackgroundService.ExecuteAsync after the first await
    private static void OnApplicationStopped(IHostApplicationLifetime applicationLifetime, IEnumerable<IHostedService> services)
    {
        var exceptions = new List<Exception>();
        foreach (var service in services)
        {
            if (service is not BackgroundService backgroundService) continue;
            var backgroundTask = backgroundService.ExecuteTask;
            if (backgroundTask == null) continue;
            var aggregateException = backgroundTask.Exception;
            if (aggregateException == null) continue;
            
            if (applicationLifetime.ApplicationStopping.IsCancellationRequested
                && backgroundTask.IsCanceled
                && aggregateException.InnerExceptions.All(e => e is TaskCanceledException))
            {
                return;
            }

            if (aggregateException.InnerExceptions.Count == 1)
            {
                exceptions.Add(aggregateException.InnerExceptions[0]);
            }
            else
            {
                exceptions.AddRange(aggregateException.InnerExceptions);
            }
        }
        
        if (exceptions.Count == 0) return;
        
        var exception = exceptions.Count == 1 ? exceptions[0] : new AggregateException(exceptions);
        CrashReportHelper.HandleFatalException(exception);
    }

    private static void UnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        CrashReportHelper.HandleFatalException((Exception)args.ExceptionObject);
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
