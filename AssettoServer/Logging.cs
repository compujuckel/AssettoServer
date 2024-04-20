﻿using AssettoServer.Server.Configuration.Extra;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;
using Serilog.Templates;

namespace AssettoServer;

internal static class Logging
{
    internal static void CreateDefaultLogger(string logPrefix, bool isContentManager, bool useVerboseLogging)
    {
        Log.Logger = BaseLoggerConfiguration(logPrefix, isContentManager, useVerboseLogging).CreateLogger();
    }

    internal static void CreateLokiLogger(string logPrefix, bool isContentManager, string? preset, LokiSettings lokiSettings, bool useVerboseLogging)
    {
        Log.Logger = BaseLoggerConfiguration(logPrefix, isContentManager, useVerboseLogging)
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
                propertiesAsLabels: new[] { "MachineName", "Preset" })
            .CreateLogger();
    }

    private static LoggerConfiguration BaseLoggerConfiguration(string logPrefix, bool isContentManager, bool useVerboseLogging)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Override("AssettoServer.Network.Http.Authentication.ACClientAuthenticationHandler",
                LogEventLevel.Warning)
            .MinimumLevel.Override("AspNetCore.Authentication.ApiKey.ApiKeyInHeaderHandler", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Grpc", LogEventLevel.Warning)
            .WriteTo.Async(a =>
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
