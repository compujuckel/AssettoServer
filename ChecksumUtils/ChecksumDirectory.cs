using AssettoServer.Shared.Checksum;

namespace ChecksumUtils;

public static class ChecksumDirectory
{
    public const string KunosFileName = "checksums_ks.json";
    public const string CustomFileName = "checksums_remote.json";

    public static (ChecksumsFile Kunos, ChecksumsFile Custom) Load(string? inputDirectory)
    {
        if (string.IsNullOrWhiteSpace(inputDirectory))
            return (new ChecksumsFile(), new ChecksumsFile());

        if (!Directory.Exists(inputDirectory))
            throw new DirectoryNotFoundException($"Checksum input directory not found: {inputDirectory}");

        return (
            LoadFileIfPresent(Path.Combine(inputDirectory, KunosFileName)),
            LoadFileIfPresent(Path.Combine(inputDirectory, CustomFileName)));
    }

    public static async Task SaveAsync(
        string outputDirectory,
        ChecksumsFile kunos,
        ChecksumsFile custom,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Checksum output directory is required", nameof(outputDirectory));

        Directory.CreateDirectory(outputDirectory);
        await SaveFileAsync(Path.Combine(outputDirectory, KunosFileName), kunos, cancellationToken);
        await SaveFileAsync(Path.Combine(outputDirectory, CustomFileName), custom, cancellationToken);
    }

    private static ChecksumsFile LoadFileIfPresent(string path)
    {
        return File.Exists(path)
            ? ChecksumsFile.FromJson(File.ReadAllText(path))
            : new ChecksumsFile();
    }

    private static async Task SaveFileAsync(
        string path,
        ChecksumsFile checksums,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(path, checksums.ToJson(), cancellationToken);
    }
}
