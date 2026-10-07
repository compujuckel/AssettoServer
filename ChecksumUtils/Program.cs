using AssettoServer.Shared.Checksum;
using CommandLine;
using Serilog;

namespace ChecksumUtils;

internal static class Program
{
    private sealed class Options
    {
        [Option('i', "input", Required = false, HelpText = "Directory containing checksums_ks.json and checksums_remote.json")]
        public string? InputDirectory { get; set; }

        [Option('o', "output", Required = true, HelpText = "Output directory for checksums_ks.json and checksums_remote.json")]
        public string OutputDirectory { get; set; } = null!;

        [Option('a', "assetto", Required = true, HelpText = "Assetto Corsa installation directory")]
        public string AssettoDirectory { get; set; } = null!;

        [Option("replace", Required = false, HelpText = "Replace existing checksums")]
        public bool Replace { get; set; } = false;
    }

    public static async Task Main(string[] args)
    {
        var options = Parser.Default.ParseArguments<Options>(args).Value;
        if (options == null)
            return;

        Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Console().CreateLogger();

        var (kunos, custom) = ChecksumDirectory.Load(options.InputDirectory);
        Log.Information("Loaded {TrackCount} Kunos tracks and {CarCount} Kunos cars, plus {CustomTrackCount} custom tracks and {CustomCarCount} custom cars",
            kunos.Tracks.Count, kunos.Cars.Count, custom.Tracks.Count, custom.Cars.Count);

        ChecksumUpdateSummary summary = ChecksumGenerator.UpdateFromLocalContent(
            kunos, custom, options.AssettoDirectory, options.Replace);
        await ChecksumDirectory.SaveAsync(options.OutputDirectory, kunos, custom);

        Log.Information("Wrote {TrackCount} Kunos tracks and {CarCount} Kunos cars, plus {CustomTrackCount} custom tracks and {CustomCarCount} custom cars to {Path}",
            kunos.Tracks.Count, kunos.Cars.Count, custom.Tracks.Count, custom.Cars.Count, options.OutputDirectory);
        Log.Information("Checksum update summary: {SkippedCount} mismatching checksum(s) skipped",
            summary.SkippedChecksums.Count);
        foreach (var skipped in summary.SkippedChecksums.OrderBy(
                     checksum => checksum.ContentPath, StringComparer.OrdinalIgnoreCase))
        {
            Log.Warning(
                "Skipped {ContentPath}: stored MD5={ExistingMd5}, local MD5={LocalMd5}, stored SHA256={ExistingSha256}, local SHA256={LocalSha256}",
                skipped.ContentPath, skipped.ExistingMd5, skipped.LocalMd5, skipped.ExistingSha256, skipped.LocalSha256);
        }
    }
}
