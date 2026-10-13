using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
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
        bool loadEmbedded = false;
        if (!File.Exists(kunosPath))
        {
            try
            {
                Directory.CreateDirectory(serverContentDirectory);
                using var checksums = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("AssettoServer.Assets.checksums_ks.json")!;
                using var outFile = File.Create(kunosPath);
                await checksums.CopyToAsync(outFile, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                loadEmbedded = true;
                Log.Warning(ex, "Could not create bundled checksum file {Path}; using the embedded checksum data", kunosPath);
            }
        }

        ChecksumsFile? remoteChecksums = await LoadRemoteChecksumsAsync(serverContentDirectory, cancellationToken);
        if (remoteChecksums != null)
            merged.MergeMissingFrom(remoteChecksums);

        try
        {
            if (loadEmbedded)
            {
                using var resource = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("AssettoServer.Assets.checksums_ks.json")!;
                var (checksums, _) = await DeserializeChecksumsAsync(resource, cancellationToken);
                merged.MergeMissingFrom(checksums);
                Log.Information("Loaded bundled Kunos checksums from the embedded resource");
            }
            else
            {
                merged.MergeMissingFrom(await LoadJsonFileAsync(kunosPath, cancellationToken));
                Log.Information("Loaded bundled Kunos checksums from {Path}", kunosPath);
            }
        }
        catch (ConfigurationParsingException ex)
        {
            Log.Error(ex, "Could not load Kunos checksums from {Path}; continuing with other checksum sources", kunosPath);
        }
        catch (Exception ex) when (loadEmbedded && ex is (IOException or JsonException or InvalidDataException))
        {
            Log.Error(ex, "Could not load embedded Kunos checksums; continuing with other checksum sources");
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

        string temporaryPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using var response = await _httpClient.GetAsync(
                RemoteChecksumsUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            using var body = await response.Content.ReadAsStreamAsync(timeout.Token);

            FileStream? cache = null;
            try
            {
                Directory.CreateDirectory(contentDirectory);
                cache = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Error(ex, "Could not create remote checksum cache at {Path}; using downloaded data without caching", cachePath);
            }

            var (checksums, cached) = await DeserializeChecksumsAsync(
                body, timeout.Token, response.Content.Headers.ContentType?.CharSet, cache, cachePath);
            if (cached)
            {
                try
                {
                    File.Move(temporaryPath, cachePath, overwrite: true);
                    Log.Information("Saved remote checksums to {Path}", cachePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Error(ex, "Fetched remote checksums but could not cache them at {Path}", cachePath);
                }
            }

            return checksums;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Error("Timed out fetching remote checksums from {Url}; continuing without remote data and retrying on the next startup",
                RemoteChecksumsUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidDataException or ArgumentException)
        {
            Log.Error(ex,
                "Could not fetch remote checksums from {Url}; continuing without remote data and retrying on the next startup",
                RemoteChecksumsUrl);
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

        return null;
    }

    public void Dispose() => _httpClient.Dispose();

    private static async Task<ChecksumsFile> LoadJsonFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var (checksums, _) = await DeserializeChecksumsAsync(stream, cancellationToken);
            return checksums;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            throw new ConfigurationParsingException(path, ex);
        }
    }

    private static async Task<(ChecksumsFile Checksums, bool Cached)> DeserializeChecksumsAsync(
        Stream source,
        CancellationToken cancellationToken,
        string? charset = null,
        FileStream? cache = null,
        string? cachePath = null)
    {
        using var input = new ChecksumReadStream(source, cache, cachePath);
        await input.InitializeAsync(charset, cancellationToken);
        ChecksumsFile? checksums;
        bool cached;
        if (input.SourceEncoding.CodePage == Encoding.UTF8.CodePage)
        {
            checksums = await JsonSerializer.DeserializeAsync<ChecksumsFile>(input, SerializerOptions, cancellationToken);
            await input.CompleteCacheAsync(cancellationToken);
            cached = input.CacheSucceeded;
        }
        else
        {
            using var utf8 = Encoding.CreateTranscodingStream(input, input.SourceEncoding, Encoding.UTF8, leaveOpen: true);
            using var transcodedInput = new ChecksumReadStream(utf8, input.DetachCache(), cachePath);
            await transcodedInput.InitializeAsync(null, cancellationToken);
            checksums = await JsonSerializer.DeserializeAsync<ChecksumsFile>(transcodedInput, SerializerOptions, cancellationToken);
            await transcodedInput.CompleteCacheAsync(cancellationToken);
            cached = transcodedInput.CacheSucceeded;
        }

        if (checksums == null)
            throw new JsonException("Cannot deserialize checksum data");

        return (checksums, cached);
    }

    private sealed class ChecksumReadStream(Stream source, FileStream? cache, string? cachePath) : Stream
    {
        private readonly byte[] _prefix = ArrayPool<byte>.Shared.Rent(4);
        private readonly bool _cacheRequested = cache != null;
        private FileStream? _cache = cache;
        private int _prefixOffset;
        private int _prefixLength;
        private bool _cacheFailed;
        private bool _disposed;

        public Encoding SourceEncoding { get; private set; } = Encoding.UTF8;
        public bool CacheSucceeded => _cacheRequested && !_cacheFailed;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public async Task InitializeAsync(string? charset, CancellationToken cancellationToken)
        {
            _prefixLength = await source.ReadAtLeastAsync(_prefix.AsMemory(0, 4), 4,
                throwOnEndOfStream: false, cancellationToken);
            ReadOnlySpan<byte> prefix = _prefix.AsSpan(0, _prefixLength);
            if (prefix.StartsWith<byte>([0xff, 0xfe, 0x00, 0x00]))
            {
                SourceEncoding = Encoding.UTF32;
                _prefixOffset = 4;
            }
            else if (prefix.StartsWith<byte>([0x00, 0x00, 0xfe, 0xff]))
            {
                SourceEncoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
                _prefixOffset = 4;
            }
            else if (prefix.StartsWith<byte>([0xef, 0xbb, 0xbf]))
            {
                _prefixOffset = 3;
            }
            else if (prefix.StartsWith<byte>([0xff, 0xfe]))
            {
                SourceEncoding = Encoding.Unicode;
                _prefixOffset = 2;
            }
            else if (prefix.StartsWith<byte>([0xfe, 0xff]))
            {
                SourceEncoding = Encoding.BigEndianUnicode;
                _prefixOffset = 2;
            }
            else if (!string.IsNullOrWhiteSpace(charset))
            {
                SourceEncoding = Encoding.GetEncoding(charset.Trim('"'));
            }

            if (SourceEncoding.CodePage == Encoding.UTF8.CodePage)
                await WriteCacheAsync(_prefix.AsMemory(0, _prefixLength), cancellationToken);
        }

        public FileStream? DetachCache()
        {
            var cacheStream = _cache;
            _cache = null;
            return cacheStream;
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (_prefixOffset < _prefixLength)
                return ReadPrefix(buffer);

            int read = source.Read(buffer);
            try
            {
                _cache?.Write(buffer[..read]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                DisableCache(ex);
            }
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_prefixOffset < _prefixLength)
                return ReadPrefix(buffer.Span);

            int read = await source.ReadAsync(buffer, cancellationToken);
            await WriteCacheAsync(buffer[..read], cancellationToken);
            return read;
        }

        private int ReadPrefix(Span<byte> buffer)
        {
            int count = Math.Min(buffer.Length, _prefixLength - _prefixOffset);
            _prefix.AsSpan(_prefixOffset, count).CopyTo(buffer);
            _prefixOffset += count;
            return count;
        }

        private async ValueTask WriteCacheAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            if (_cache == null)
                return;

            try
            {
                await _cache.WriteAsync(bytes, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                DisableCache(ex);
            }
        }

        public async Task CompleteCacheAsync(CancellationToken cancellationToken)
        {
            if (_cache == null)
                return;

            try
            {
                await _cache.FlushAsync(cancellationToken);
                await _cache.DisposeAsync();
                _cache = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                DisableCache(ex);
            }
        }

        private void DisableCache(Exception exception)
        {
            _cacheFailed = true;
            Log.Error(exception, "Could not write remote checksum cache at {Path}; using downloaded data without caching", cachePath);
            DisposeCache();
        }

        private void DisposeCache()
        {
            try
            {
                _cache?.Dispose();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _cacheFailed = true;
                Log.Warning(ex, "Could not close remote checksum cache at {Path}", cachePath);
            }
            _cache = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                DisposeCache();
                ArrayPool<byte>.Shared.Return(_prefix);
            }
            base.Dispose(disposing);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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

    private static void SkipValue(ref Utf8JsonReader reader)
    {
        if (!reader.TrySkip())
            throw new JsonException("Incomplete checksum value");
    }

    private sealed class ChecksumDictionaryJsonConverter<TEntry> : JsonConverter<Dictionary<string, TEntry>>
        where TEntry : class
    {
        public override bool HandleNull => true;

        public override Dictionary<string, TEntry> Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var entries = new Dictionary<string, TEntry>(StringComparer.OrdinalIgnoreCase);
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                SkipValue(ref reader);
                Log.Warning("Skipping invalid {EntryType} checksum collection", typeof(TEntry).Name);
                return entries;
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return entries;
                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Expected a checksum entry name");

                string name = reader.GetString()!;
                if (!reader.Read())
                    throw new JsonException("Expected a checksum entry");

                var end = reader;
                SkipValue(ref end);
                try
                {
                    var entry = JsonSerializer.Deserialize<TEntry>(ref reader, options);
                    if (entry == null)
                    {
                        Log.Warning("Skipping empty checksum entry {Entry}", name);
                        continue;
                    }

                    entries[name] = entry;
                }
                catch (JsonException ex)
                {
                    Log.Warning(ex, "Skipping invalid checksum entry {Entry}", name);
                }
                finally
                {
                    reader = end;
                }
            }

            throw new JsonException("Incomplete checksum collection");
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
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                SkipValue(ref reader);
                Log.Warning("Skipping invalid checksum value");
                return null;
            }

            var checksum = new ChecksumValue();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return checksum.MD5.HasValue || checksum.SHA256.HasValue ? checksum : null;
                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Expected a checksum algorithm name");

                int algorithm = reader.ValueTextEquals("MD5"u8) ? 0 : reader.ValueTextEquals("SHA256"u8) ? 1 : -1;
                if (algorithm < 0)
                {
                    string name = reader.GetString()!;
                    if (name.Equals("MD5", StringComparison.OrdinalIgnoreCase))
                        algorithm = 0;
                    else if (name.Equals("SHA256", StringComparison.OrdinalIgnoreCase))
                        algorithm = 1;
                }
                if (!reader.Read())
                    throw new JsonException("Expected a checksum digest");

                var end = reader;
                SkipValue(ref end);
                try
                {
                    if (algorithm == 0)
                        checksum.MD5 = JsonSerializer.Deserialize<Md5Checksum?>(ref reader, options);
                    else if (algorithm == 1)
                        checksum.SHA256 = JsonSerializer.Deserialize<Sha256Checksum?>(ref reader, options);
                }
                catch (JsonException ex)
                {
                    Log.Warning(ex, "Skipping invalid {Algorithm} checksum", algorithm == 0 ? "MD5" : "SHA256");
                }
                finally
                {
                    reader = end;
                }
            }

            throw new JsonException("Incomplete checksum value");
        }

        public override void Write(Utf8JsonWriter writer, ChecksumValue value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value);
        }
    }
}
