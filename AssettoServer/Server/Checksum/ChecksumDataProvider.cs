using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Checksum;
using Serilog;

namespace AssettoServer.Server.Checksum;

public sealed class ChecksumDataProvider : IDisposable
{
    public const string RemoteChecksumsUrl =
        "https://raw.githubusercontent.com/compujuckel/AssettoServer/master/AssettoServer/Assets/checksums_remote.json";

    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(15);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        Converters = { new ChecksumDictionaryJsonConverterFactory(), new ChecksumValueJsonConverter() }
    };

    private readonly HttpClient _httpClient;
    private readonly ACServerConfiguration _serverConfiguration;

    public ChecksumDataProvider(ACServerConfiguration serverConfiguration)
    {
        _httpClient = new HttpClient { Timeout = DownloadTimeout };
        _serverConfiguration = serverConfiguration;
    }

    public async Task<ChecksumsFile> LoadAsync(CancellationToken cancellationToken = default)
    {
        var merged = new ChecksumsFile();
        if (!_serverConfiguration.Extra.EnablePreloadedChecksums)
        {
            Log.Information("Preloaded checksums are disabled");
            return merged;
        }

        string serverContentDirectory = Path.Combine(Environment.CurrentDirectory, "content");
        string kunosPath = Path.Combine(serverContentDirectory, "checksums_ks.json");
        if (!File.Exists(kunosPath))
        {
            try
            {
                Directory.CreateDirectory(serverContentDirectory);
                using var checksums = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("AssettoServer.Assets.checksums_ks.json")!;
                using var outFile = File.Create(kunosPath);
                checksums.CopyTo(outFile);
            }
            catch (Exception ex)
            {
                throw new ConfigurationParsingException(kunosPath, ex);
            }
        }

        ChecksumsFile? remoteChecksums = await LoadRemoteChecksumsAsync(serverContentDirectory, cancellationToken);
        if (remoteChecksums != null)
            merged.MergeMissingFrom(remoteChecksums);

        try
        {
            merged.MergeMissingFrom(await LoadJsonFileAsync(kunosPath, cancellationToken));
            Log.Information("Loaded bundled Kunos checksums from {Path}", kunosPath);
        }
        catch (ConfigurationParsingException ex)
        {
            Log.Error(ex, "Could not load Kunos checksums from {Path}; continuing with other checksum sources", kunosPath);
        }

        return merged;
    }

    private async Task<ChecksumsFile?> LoadRemoteChecksumsAsync(string contentDirectory, CancellationToken cancellationToken)
    {
        string cachePath = Path.Combine(contentDirectory, "checksums_remote.json");
        if (File.Exists(cachePath))
        {
            Log.Information("Reusing cached remote checksums from {Path}", cachePath);
            try
            {
                return await LoadJsonFileAsync(cachePath, cancellationToken);
            }
            catch (ConfigurationParsingException ex)
            {
                Log.Error(ex, "Could not load remote checksum cache {Path}; skipping the cache and continuing without remote checksum data",
                    cachePath);
                return null;
            }
        }

        Log.Information("Fetching remote checksums from {Url}", RemoteChecksumsUrl);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DownloadTimeout);

        try
        {
            using var response = await _httpClient.GetAsync(RemoteChecksumsUrl, timeout.Token);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(timeout.Token);
            ChecksumsFile checksums = DeserializeChecksums(json);

            string temporaryPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                Directory.CreateDirectory(contentDirectory);
                await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
                File.Move(temporaryPath, cachePath, overwrite: true);
                Log.Information("Saved remote checksums to {Path}", cachePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Error(ex, "Fetched remote checksums but could not cache them at {Path}", cachePath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Log.Warning(ex, "Could not remove temporary checksum cache {Path}", temporaryPath);
                    }
                }
            }

            return checksums;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Error("Timed out fetching remote checksums from {Url}; continuing without remote data and retrying on the next startup",
                RemoteChecksumsUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidDataException)
        {
            Log.Error(ex,
                "Could not fetch remote checksums from {Url}; continuing without remote data and retrying on the next startup",
                RemoteChecksumsUrl);
        }

        return null;
    }

    public void Dispose() => _httpClient.Dispose();

    private static async Task<ChecksumsFile> LoadJsonFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            string json = await File.ReadAllTextAsync(path, cancellationToken);
            return DeserializeChecksums(json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            throw new ConfigurationParsingException(path, ex);
        }
    }

    private static ChecksumsFile DeserializeChecksums(string json)
    {
        return JsonSerializer.Deserialize<ChecksumsFile>(json, SerializerOptions)
               ?? throw new JsonException("Cannot deserialize checksum data");
    }

    private sealed class ChecksumDictionaryJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            if (!typeToConvert.IsGenericType || typeToConvert.GetGenericTypeDefinition() != typeof(Dictionary<,>))
                return false;

            var arguments = typeToConvert.GetGenericArguments();
            return arguments[0] == typeof(string)
                   && (typeof(ChecksumEntry).IsAssignableFrom(arguments[1])
                       || arguments[1] == typeof(ChecksumValue)
                       || arguments[1] == typeof(SurfaceChecksumVariants));
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var entryType = typeToConvert.GetGenericArguments()[1];
            return (JsonConverter)Activator.CreateInstance(
                typeof(ChecksumDictionaryJsonConverter<>).MakeGenericType(entryType), nonPublic: true)!;
        }
    }

    private sealed class ChecksumDictionaryJsonConverter<TEntry> : JsonConverter<Dictionary<string, TEntry>>
        where TEntry : class
    {
        public override bool HandleNull => true;

        public override Dictionary<string, TEntry> Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var entries = new Dictionary<string, TEntry>(StringComparer.OrdinalIgnoreCase);
            using var document = JsonDocument.ParseValue(ref reader);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                Log.Warning("Skipping invalid {EntryType} checksum collection", typeof(TEntry).Name);
                return entries;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                try
                {
                    var entry = property.Value.Deserialize<TEntry>(options);
                    if (entry == null)
                    {
                        Log.Warning("Skipping empty checksum entry {Entry}", property.Name);
                        continue;
                    }

                    entries[property.Name] = entry;
                }
                catch (JsonException ex)
                {
                    Log.Warning(ex, "Skipping invalid checksum entry {Entry}", property.Name);
                }
            }

            return entries;
        }

        public override void Write(
            Utf8JsonWriter writer, Dictionary<string, TEntry> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value);
        }
    }

    private sealed class ChecksumValueJsonConverter : JsonConverter<ChecksumValue>
    {
        public override ChecksumValue? Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                Log.Warning("Skipping invalid checksum value");
                return null;
            }

            var checksum = new ChecksumValue();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                try
                {
                    if (property.Name.Equals("MD5", StringComparison.OrdinalIgnoreCase))
                        checksum.MD5 = property.Value.Deserialize<Md5Checksum?>();
                    else if (property.Name.Equals("SHA256", StringComparison.OrdinalIgnoreCase))
                        checksum.SHA256 = property.Value.Deserialize<Sha256Checksum?>();
                }
                catch (JsonException ex)
                {
                    Log.Warning(ex, "Skipping invalid {Algorithm} checksum", property.Name);
                }
            }

            return checksum.MD5.HasValue || checksum.SHA256.HasValue ? checksum : null;
        }

        public override void Write(Utf8JsonWriter writer, ChecksumValue value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value);
        }
    }
}
