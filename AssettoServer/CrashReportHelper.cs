using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AssettoServer.Server.Configuration;
using Scriban;

namespace AssettoServer;

public static partial class CrashReportHelper
{
    private class Attachment
    {
        public required string Name { get; init; }
        public string? Type { get; init; }
        public required string Content { get; init; }
    }
    
    public static void GenerateCrashReport(ConfigurationLocations locations, Exception exception)
    {
        Template template;
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AssettoServer.Assets.crash_report.md.tpl")!)
        using (var streamReader = new StreamReader(stream))
        {
            template = Template.Parse(streamReader.ReadToEnd());
        }
        
        Directory.CreateDirectory("crash");
        var filename = $"crash_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.txt";
        var result = template.Render(new
        {
            Timestamp = DateTime.Now.ToString("g"),
            ServerVersion = ThisAssembly.AssemblyInformationalVersion,
            OsVersion = Environment.OSVersion.VersionString,
            CpuArchitecture = RuntimeInformation.ProcessArchitecture,
            ContentManager = Program.IsContentManager,
            Attachments = new List<Attachment>
            {
                new()
                {
                    Name = "Exception",
                    Content = $"{exception}\n{exception.StackTrace}"
                },
                new()
                {
                    Name = "extra_cfg.yml",
                    Type = "yml",
                    Content = RedactFile(File.ReadAllText(locations.ExtraCfgPath))
                },
                new()
                {
                    Name = "server_cfg.ini",
                    Type = "ini",
                    Content = RedactFile(File.ReadAllText(locations.ServerCfgPath))
                },
                new()
                {
                    Name = "entry_list.ini",
                    Type = "ini",
                    Content = RedactFile(File.ReadAllText(locations.EntryListPath))
                } 
            }
        });
        
        File.WriteAllText(Path.Join("crash", filename), result);
    }

    [GeneratedRegex(@"((?:password|key|token|url)\s*[=:][ \t]*)(.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveDataRegex(); 

    private static string RedactFile(string text)
    {
        return SensitiveDataRegex().Replace(text, "$1redacted");
    }
}
