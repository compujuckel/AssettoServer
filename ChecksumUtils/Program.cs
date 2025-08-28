using AssettoServer.Server.Checksum;
using CommandLine;
using JetBrains.Annotations;
using Serilog;

namespace ChecksumUtils;

internal static class Program
{
    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
    private class Options
    {
        [Option('i', "input", Required = true, HelpText = "Input file")]
        public string Input { get; set; } = null!;
        
        [Option('o', "output", Required = true, HelpText = "Output file")]
        public string Output { get; set; } = null!;
        
        [Option('a', "assetto", Required = true, HelpText = "AC base directory")]
        public string AssettoBaseDir { get; set; } = null!;

        [Option("replace", Required = false, HelpText = "Replace existing checksums")]
        public bool Replace { get; set; } = false;
    }
    
    public static async Task Main(string[] args)
    {
        var options = Parser.Default.ParseArguments<Options>(args).Value;
        if (options == null) return;
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        Log.Information("Reading {Path}...", options.Input);
        var checksums = ChecksumsFile.FromFile(options.Input);
        Log.Information("Found {Tracks} tracks and {Cars} cars in the input file", checksums.Tracks.Count, checksums.Cars.Count);
        
        if (options.Replace)
            Log.Warning("Any file found in the local installation of Assetto Corsa will overwrite existing checksums");

        checksums.AddNewSums(options.AssettoBaseDir, options.Replace);

        Log.Information("Finished with {Tracks} tracks and {Cars} cars", checksums.Tracks.Count, checksums.Cars.Count);
        Log.Information("Writing output to {Path}...", options.Output);
        await using (var outputFile = File.CreateText(options.Output))
        {
            checksums.ToFile(outputFile);
        }
        
        Log.Information("Finished");
    }
}
