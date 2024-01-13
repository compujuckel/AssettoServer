﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using Autofac.Core;
using IniParser.Exceptions;
using Microsoft.AspNetCore.Connections;
using YamlDotNet.Core;

namespace AssettoServer;

internal static class ExceptionHelper
{
    private const string HorizontalSeparator = "══════════════════════════════════════════════════════════════════════════════════════════════════════";

    private const string GeneralInformation = """
                                              AssettoServer has failed to start. Here is what to try next:
                                              - Read the above error message carefully
                                              - Read the documentation: https://assettoserver.org/
                                              - Ask for help in the official AssettoServer Discord: https://discord.gg/uXEXRcSkyz

                                              Please make sure that you downloaded AssettoServer from the official website.
                                              Security of the files cannot be guaranteed if you downloaded it from a random YouTube video
                                              or another Discord server.
                                              """;

    public static void PrintExceptionHelp(Exception ex, bool isContentManager, string? crashReportPath)
    {
        string? helpLink = null;

        while (ex is DependencyResolutionException && ex.InnerException != null)
        {
            ex = ex.InnerException;
        }
        
        Console.WriteLine();
        switch (ex)
        {
            case YamlException yamlException:
                WrapText(YamlExceptionHelp(yamlException), isContentManager);
                break;
            case ParsingException when ex.InnerException is FileNotFoundException fnfException:
                WrapText(fnfException.Message, isContentManager);
                break;
            case ParsingException iniException:
                WrapText(IniExceptionHelp(iniException), isContentManager);
                break;
            case IOException { InnerException: AddressInUseException } or SocketException { ErrorCode: 10048 }:
                WrapText(AddressInUseExceptionHelp(), isContentManager);
                break;
            case ConfigurationException configurationException:
                WrapText(configurationException.Message, isContentManager);
                helpLink = configurationException.HelpLink;
                break;
            default:
                WrapText(ex.Message, isContentManager);
                break;
        }

        Console.WriteLine(HorizontalSeparator);
        Console.WriteLine(GeneralInformation);
        Console.WriteLine(HorizontalSeparator);
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !Console.IsInputRedirected && !isContentManager)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            if (helpLink != null)
            {
                Console.WriteLine("Press I to get more info on this error");
            }
            if (crashReportPath != null)
            {
                Console.WriteLine("Press C to show the crash report");
            }
            Console.WriteLine("Press W to go to the AssettoServer website");
            Console.WriteLine("Press D to join the official Discord server");
            Console.WriteLine("Press any other key to exit");

            if (!Program.IsDebugBuild)
            {
                var key = Console.ReadKey();
                if (key.Key == ConsoleKey.D)
                {
                    OpenURL("https://discord.gg/uXEXRcSkyz");
                }
                else if (helpLink != null && key.Key == ConsoleKey.I)
                {
                    OpenURL(helpLink);
                }
                else if (key.Key == ConsoleKey.W)
                {
                    OpenURL("https://assettoserver.org/");
                }
                else if (crashReportPath != null && key.Key == ConsoleKey.C)
                {
                    Process.Start("explorer.exe", $"/select, \"{crashReportPath}\"");
                }
            }

            Console.ForegroundColor = old;
        }
    }

    private static void WrapText(string text, bool isContentManager)
    {
        Console.WriteLine(HorizontalSeparator);
        var old = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        if (isContentManager)
        {
            var reader = new StringReader(text);
            while (reader.ReadLine() is { } line)
            {
                Console.Write("▲ ");
                Console.WriteLine(line);
            }
        }
        else
        {
            Console.WriteLine(text);
        }

        Console.ForegroundColor = old;
    }

    private static string YamlExceptionHelp(YamlException ex)
    {
        return $"""
YAML error. Check your extra_cfg.yml around line {ex.Start.Line}.
When copying plugin configuration, make sure to copy EVERYTHING, including the "---".
{ex.Message}
""";
    }

    private static string IniExceptionHelp(ParsingException ex)
    {
        return $"""
INI error. Check your server_cfg.ini or entry_list.ini around line {ex.LineNumber}.
{ex.Message}
""";
    }

    private static string AddressInUseExceptionHelp()
    {
        return """
There is already a server running on the same ports.
When hosting through Content Manager, make sure that server presets using the same ports are stopped.
You can also check in Task Manager if another AssettoServer.exe process is running and stop it there. 
""";
    }

    private static void OpenURL(string url)
    {
        url = url.Replace("&", "^&");
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
