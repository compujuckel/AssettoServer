using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Utils;
using Autofac.Extensions.DependencyInjection;
using DotNext.Collections.Generic;
using FastLaneUtils;
using FluentValidation;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;
using Serilog;

namespace AssettoServer;

public static class Program
{
#if DEBUG
    public static readonly bool IsDebugBuild = true;
#else
    public static readonly bool IsDebugBuild = false;
#endif
    
    private class Options
    {
        public string Preset { get; set; } = "";
        public string ServerCfgPath { get; set; } = "";
        public string EntryListPath { get; set; } = "";
        public bool LoadPluginsFromWorkdir { get; set; } = false;
        public bool UseVerboseLogging { get; set; } = false;
        public bool UseRandomPreset { get; set; } = false;
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
    private static TaskCompletionSource<StartOptions> _restartTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
    
    internal static async Task<int> Main(string[] args)
    {
        SetupFluentValidation();
        SetupMetrics();
        DetectContentManager();
        
        var rootCommand = CreateRootCommand();
        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static RootCommand CreateRootCommand()
    {
        var configOptions = CreateConfigurationOptions();
        var useVerboseLoggingOption = new Option<bool>("--verbose") { Description = "Change log level to verbose" };
        var useRandomPresetOption = new Option<bool>("--use-random-preset", "-r") { Description = "Use a random available configuration preset" };

        var rootCommand = new RootCommand();
        AddConfigurationOptions(rootCommand, configOptions);
        rootCommand.Options.Add(useVerboseLoggingOption);
        rootCommand.Options.Add(useRandomPresetOption);

        rootCommand.Subcommands.Add(FastLaneCommand.CreateCommand());
        rootCommand.Subcommands.Add(CreateGenerateConfigCommand());

        rootCommand.SetAction(async (parseResult, _) =>
        {
            await RunAsync(new Options
            {
                Preset = parseResult.GetValue(configOptions.Preset) ?? "",
                ServerCfgPath = parseResult.GetValue(configOptions.ServerCfgPath) ?? "",
                EntryListPath = parseResult.GetValue(configOptions.EntryListPath) ?? "",
                LoadPluginsFromWorkdir = parseResult.GetValue(configOptions.LoadPluginsFromWorkdir),
                UseVerboseLogging = parseResult.GetValue(useVerboseLoggingOption),
                UseRandomPreset = parseResult.GetValue(useRandomPresetOption),
            });
            return 0;
        });

        return rootCommand;
    }

    private sealed record ConfigurationOptions(
        Option<string?> Preset,
        Option<string?> ServerCfgPath,
        Option<string?> EntryListPath,
        Option<bool> LoadPluginsFromWorkdir);

    private static ConfigurationOptions CreateConfigurationOptions()
    {
        return new ConfigurationOptions(
            new Option<string?>("--preset", "-p") { Description = "Configuration preset" },
            new Option<string?>("-c") { Description = "Path to server configuration" },
            new Option<string?>("-e") { Description = "Path to entry list" },
            new Option<bool>("--plugins-from-workdir") { Description = "Additionally load plugins from working directory" });
    }

    private static void AddConfigurationOptions(Command command, ConfigurationOptions options)
    {
        command.Options.Add(options.Preset);
        command.Options.Add(options.ServerCfgPath);
        command.Options.Add(options.EntryListPath);
        command.Options.Add(options.LoadPluginsFromWorkdir);
    }

    private static Command CreateGenerateConfigCommand()
    {
        var configOptions = CreateConfigurationOptions();

        var command = new Command("generate-config", "Generate configuration file for all installed plugins");
        AddConfigurationOptions(command, configOptions);

        command.SetAction((parseResult, _) =>
        {
            GenerateConfigs(
                parseResult.GetValue(configOptions.Preset),
                parseResult.GetValue(configOptions.ServerCfgPath),
                parseResult.GetValue(configOptions.EntryListPath),
                parseResult.GetValue(configOptions.LoadPluginsFromWorkdir));
            return Task.FromResult(0);
        });

        return command;
    }

    private static void GenerateConfigs(string? preset, string? serverCfgPath, string? entryListPath, bool loadPluginsFromWorkdir)
    {
        ConfigurationLocations = ConfigurationLocations.FromOptions(preset, serverCfgPath, entryListPath);

        Logging.CreateLogger(IsContentManager, preset, false);

        var config = new ACServerConfiguration(preset, ConfigurationLocations, loadPluginsFromWorkdir, portOverrides: null);

        var loader = new ACPluginLoader(loadPluginsFromWorkdir);
        loader.LoadPlugins(loader.AvailablePlugins.Select(p => p.Key).ToList());
        config.LoadPluginConfiguration(loader, null);

        Log.Information("Plugin configuration generated in {Path}", config.BaseFolder);
    }

    private static async Task RunAsync(Options options)
    {
        _loadPluginsFromWorkdir = options.LoadPluginsFromWorkdir;
        
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

        Logging.CreateLogger(IsContentManager, options.Preset, options.UseVerboseLogging);
        
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
            _restartTask = new TaskCompletionSource<StartOptions>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource();
            var serverTask = RunServerAsync(startOptions.Preset, startOptions.ServerCfgPath, startOptions.EntryListPath, startOptions.PortOverrides ,options.UseVerboseLogging, cts.Token);
            var finishedTask = await Task.WhenAny(serverTask, _restartTask.Task);

            if (finishedTask == _restartTask.Task)
            {
                await cts.CancelAsync();
                await serverTask;

                startOptions = await _restartTask.Task;
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
            var config = new ACServerConfiguration(preset, ConfigurationLocations, _loadPluginsFromWorkdir, portOverrides);

            Logging.CreateLogger(IsContentManager, preset, useVerboseLogging, config.Extra.RedactIpAddresses, config.Extra.LokiSettings);

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
                continue;
            }
            
            exceptions.AddRange(aggregateException.InnerExceptions);
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
        if (!OperatingSystem.IsWindows()) return;
        
        try
        {
            var parentId = Process.GetCurrentProcess().ParentProcessId;
            IsContentManager = Process.GetProcessById(parentId).ProcessName == "Content Manager";
        }
        catch (Exception)
        {
            #if DEBUG
            throw;
            #endif
        }
    }
}
