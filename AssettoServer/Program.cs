using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using System;
using System.Globalization;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.Formatters.Prometheus;
using AssettoServer.Network.Http;
using AssettoServer.Server.Plugin;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using CommandLine;
using JetBrains.Annotations;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;

namespace AssettoServer;

internal static class Program
{
    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
    private class Options
    {
        [Option('p', "preset", Required = false, SetName = "AssettoServer", HelpText = "Configuration preset")]
        public string Preset { get; set; } = "";

        [Option('c', Required = false, SetName = "Content Manager compatibility", HelpText = "Path to server configuration")]
        public string ServerCfgPath { get; set; } = "";

        [Option('e', Required = false, SetName = "Content Manager compatibility", HelpText = "Path to entry list")]
        public string EntryListPath { get; set; } = "";
    }
        
    internal static async Task Main(string[] args)
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
            .MinimumLevel.Override("Grpc", LogEventLevel.Warning)
            .WriteTo.Async(a => a.Console())
            .WriteTo.File($"logs/{logPrefix}-.txt",
                rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var config = new ACServerConfiguration(options.Preset, options.ServerCfgPath, options.EntryListPath);
        
        if (config.Extra.LokiSettings != null
            && !string.IsNullOrEmpty(config.Extra.LokiSettings.Url)
            && !string.IsNullOrEmpty(config.Extra.LokiSettings.Login)
            && !string.IsNullOrEmpty(config.Extra.LokiSettings.Password))
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Grpc", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("Preset", options.Preset)
                .WriteTo.GrafanaLoki(config.Extra.LokiSettings.Url,
                    credentials: new LokiCredentials
                    {
                        Login = config.Extra.LokiSettings.Login,
                        Password = config.Extra.LokiSettings.Password
                    },
                    createLevelLabel: true,
                    useInternalTimestamp: true,
                    filtrationMode: LokiLabelFiltrationMode.Include,
                    filtrationLabels: new [] { "MachineName", "Preset" },
                    textFormatter: new LokiJsonTextFormatter(),
                    outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Async(a => a.Console())
                .WriteTo.File($"logs/{logPrefix}-.txt",
                    rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        Log.Information("AssettoServer {Version}", ThisAssembly.AssemblyInformationalVersion);
        if (!string.IsNullOrEmpty(options.Preset))
        {
            Log.Information("Using preset {Preset}", options.Preset);
        }
        
        var host = Host.CreateDefaultBuilder()
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .UseSerilog()
            .ConfigureWebHostDefaults(webHostBuilder =>
            {
                webHostBuilder.ConfigureKestrel(serverOptions => serverOptions.AllowSynchronousIO = true)
                    .UseStartup(_ => new Startup(config))
                    .UseUrls($"http://0.0.0.0:{config.Server.HttpPort}");
            })
            .Build();
        
        await host.RunAsync();
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        Log.Fatal((Exception)args.ExceptionObject, "Unhandled exception occurred");
        Log.CloseAndFlush();
        Environment.Exit(1);
    }
}
