using System.CommandLine;
using Serilog;

namespace FastLaneUtils;

public static class FastLaneCommand
{
    private class Options
    {
        public required string Input { get; init; }
        public required string Output { get; init; }
        public int CutStart { get; init; }
        public int CutEnd { get; init; }
        public bool Optimize { get; init; }
        public bool Legacy { get; init; }
        public bool Reverse { get; init; }
        public float HeightOffset { get; init; }
    }

    public static Command CreateCommand()
    {
        var inputOption = new Option<string>("--input", "-i") { Description = "Input file", Required = true };
        var outputOption = new Option<string>("--output", "-o") { Description = "Output file", Required = true };
        var cutStartOption = new Option<int>("--start") { Description = "Set point as new spline start point" };
        var cutEndOption = new Option<int>("--end") { Description = "Set point as new spline end point" };
        var optimizeOption = new Option<bool>("--optimize") { Description = "Use file format optimized for AssettoServer" };
        var legacyOption = new Option<bool>("--legacy") { Description = "Use legacy file format compatible with AC (v7)" };
        var reverseOption = new Option<bool>("--reverse") { Description = "Reverse direction of spline" };
        var heightOffsetOption = new Option<float>("--height-offset") { Description = "Change the height of all spline points (meters)" };

        var command = new Command("fast-lane", "Edit AI spline files");
        command.Options.Add(inputOption);
        command.Options.Add(outputOption);
        command.Options.Add(cutStartOption);
        command.Options.Add(cutEndOption);
        command.Options.Add(optimizeOption);
        command.Options.Add(legacyOption);
        command.Options.Add(reverseOption);
        command.Options.Add(heightOffsetOption);

        command.SetAction(async (parseResult, _) =>
        {
            await RunAsync(new Options
            {
                Input = parseResult.GetRequiredValue(inputOption),
                Output = parseResult.GetRequiredValue(outputOption),
                CutStart = parseResult.GetValue(cutStartOption),
                CutEnd = parseResult.GetValue(cutEndOption),
                Optimize = parseResult.GetValue(optimizeOption),
                Legacy = parseResult.GetValue(legacyOption),
                Reverse = parseResult.GetValue(reverseOption),
                HeightOffset = parseResult.GetValue(heightOffsetOption),
            });
            return 0;
        });

        return command;
    }

    private static async Task RunAsync(Options options)
    {
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
