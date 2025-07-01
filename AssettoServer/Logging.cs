using AssettoServer.Server.Configuration.Extra;
using AssettoServer.Utils;
using Serilog;
using Serilog.Enrichers.Sensitive;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;
using Serilog.Templates;

namespace AssettoServer;

internal static class Logging
{
    internal static void CreateLogger(string logPrefix, bool isContentManager, string? preset, bool useVerboseLogging, bool redactIpAddresses = false, LokiSettings? lokiSettings = null)
    {
        Log.CloseAndFlush();
        
        if (lokiSettings?.IsValid() == true)
        {
            CreateLokiLogger(logPrefix, isContentManager, preset, lokiSettings, useVerboseLogging, redactIpAddresses);
        }
        else
        {
            Log.Logger = BaseLoggerConfiguration(logPrefix, isContentManager, useVerboseLogging, redactIpAddresses).CreateLogger();
        }
    }

    private static void CreateLokiLogger(string logPrefix, bool isContentManager, string? preset, LokiSettings lokiSettings, bool useVerboseLogging, bool redactIpAddresses)
    {
        Log.Logger = BaseLoggerConfiguration(logPrefix, isContentManager, useVerboseLogging, redactIpAddresses)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Preset", preset ?? "")
            .WriteTo.GrafanaLoki(lokiSettings.Url!,
                credentials: new LokiCredentials
                {
                    Login = lokiSettings.Login!,
                    Password = lokiSettings.Password!
                },
                useInternalTimestamp: true,
                textFormatter: new LokiJsonTextFormatter(),
                propertiesAsLabels: ["MachineName", "Preset"])
            .CreateLogger();
    }

    private static LoggerConfiguration BaseLoggerConfiguration(string logPrefix, bool isContentManager, bool useVerboseLogging, bool redactIpAddresses)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Override("AssettoServer.Network.Http.Authentication.ACClientAuthenticationHandler",
                LogEventLevel.Warning)
            .MinimumLevel.Override("AspNetCore.Authentication.ApiKey.ApiKeyInHeaderHandler", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Grpc", LogEventLevel.Warning)
            // We do our own logging for these exceptions. The default Microsoft log statements are a bit too confusing for users
            .Filter.ByExcluding("EventId.Name = 'HostedServiceStartupFaulted' or EventId.Name = 'BackgroundServiceFaulted' or EventId.Name = 'BackgroundServiceStoppingHost'");

        if (redactIpAddresses)
        {
            loggerConfiguration.Enrich.WithSensitiveDataMasking(o =>
            {
                o.ExcludeProperties.Add("ServerInviteLink");
                o.ExcludeProperties.Add("RouterPage");
                o.ExcludeProperties.Add("LocalIp");
                o.MaskingOperators = [new IpAddressMaskingOperator()];
            });
        }
        
        loggerConfiguration.WriteTo.Async(a =>
            {
                if (isContentManager)
                {
                    a.Console(new ExpressionTemplate(
                        "{#if @l = 'Debug'}…{#else if @l = 'Warning'}‽{#else if @l = 'Error' or @l = 'Fatal'}▲{#else} {#end} {@m}\n{@x}"));
                }
                else
                {
                    a.Console();
                }
            })
            .WriteTo.File($"logs/{logPrefix}-.txt", rollingInterval: RollingInterval.Day);

        if (useVerboseLogging)
            loggerConfiguration.MinimumLevel.Verbose();
        else
            loggerConfiguration.MinimumLevel.Debug();

        return loggerConfiguration;
    }
}
