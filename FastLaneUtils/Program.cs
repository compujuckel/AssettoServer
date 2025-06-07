using CommandLine;
using JetBrains.Annotations;
using Serilog;

namespace FastLaneUtils;

internal static class Program
{
    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
    private class Options
    {
        [Option('i', "input", Required = true, HelpText = "Input file")]
        public string Input { get; set; } = null!;
        
        [Option('o', "output", Required = true, HelpText = "Output file")]
        public string Output { get; set; } = null!;

        [Option("start", Required = false, HelpText = "Set point as new spline start point")]
        public int CutStart { get; set; } = 0;
        
        [Option("end", Required = false, HelpText = "Set point as new spline end point")]
        public int CutEnd { get; set; } = 0;

        [Option("optimize", Required = false, HelpText = "Use file format optimized for AssettoServer")]
        public bool Optimize { get; set; } = false;

        [Option("legacy", Required = false, HelpText = "Use legacy file format compatible with AC (v7)")]
        public bool Legacy { get; set; } = false;

        [Option("reverse", Required = false, HelpText = "Reverse direction of spline")]
        public bool Reverse { get; set; } = false;
        [Option("height-offset", Required = false, HelpText = "Change the height of all spline points (meters)")]
        public float HeightOffset { get; set; } = 0;
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
        var fastLane = FastLane.FromFile(options.Input);
        
        Log.Information("Adjusting spline...");
        fastLane.Cut(options.CutStart, options.CutEnd);

        if (options.Optimize)
            fastLane.Version = -1;

        if (options.Legacy)
            fastLane.Version = 7;
        
        if (options.Reverse)
            fastLane.Reverse();

        if (options.HeightOffset != 0)
            fastLane.ChangeHeight(options.HeightOffset);

        Log.Information("Writing output spline to {Path}...", options.Output);
        await using (var outputFile = File.Create(options.Output))
        {
            fastLane.ToFile(outputFile);
        }
        
        Log.Information("Finished");
    }
}
