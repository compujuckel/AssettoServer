using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AssettoServer.Server;
using AssettoServer.Server.Checksum;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Configuration.Extra;
using AssettoServer.Shared.Checksum;
using ChecksumUtils;

namespace AssettoServer.Tests;

[NonParallelizable]
public class ChecksumTests
{
    [Test]
    public async Task ProviderCreatesBundledChecksumsInWorkingDirectory()
    {
        using var directory = new TemporaryDirectory();
        using var currentDirectory = new CurrentDirectoryScope(directory.Path);
        string contentDirectory = Path.Combine(directory.Path, "content");
        Directory.CreateDirectory(contentDirectory);
        await File.WriteAllTextAsync(Path.Combine(contentDirectory, "checksums_remote.json"), "{}");
        using var provider = new ChecksumDataProvider(CreateServerConfiguration());
        using var resource = typeof(ChecksumDataProvider).Assembly
            .GetManifestResourceStream("AssettoServer.Assets.checksums_ks.json")!;
        using var reader = new StreamReader(resource);
        string bundledJson = await reader.ReadToEndAsync();

        ChecksumsFile loaded = await provider.LoadAsync();
        string savedJson = await File.ReadAllTextAsync(Path.Combine(contentDirectory, "checksums_ks.json"));
        var bundled = ChecksumsFile.FromJson(bundledJson);

        Assert.Multiple(() =>
        {
            Assert.That(savedJson, Is.EqualTo(bundledJson));
            Assert.That(loaded.Tracks.Count, Is.EqualTo(bundled.Tracks.Count));
            Assert.That(loaded.Cars.Count, Is.EqualTo(bundled.Cars.Count));
        });
    }

    [Test]
    public async Task ProviderMergesByPriorityAndReusesCachedRemoteChecksums()
    {
        using var directory = new TemporaryDirectory();
        using var currentDirectory = new CurrentDirectoryScope(directory.Path);
        string serverContent = Path.Combine(directory.Path, "content");
        Directory.CreateDirectory(serverContent);
        string kunosPath = Path.Combine(serverContent, "checksums_ks.json");

        var kunos = new ChecksumsFile();
        kunos.Tracks["shared"] = new TrackChecksum
        {
            Files = { ["models.ini"] = new ChecksumValue { MD5 = Md5("ks"), SHA256 = Sha256("ks-sha") } }
        };
        kunos.Tracks["bundled_only"] = new TrackChecksum
        {
            Files = { ["models.ini"] = ChecksumForContent("bundled-only") }
        };
        await File.WriteAllTextAsync(kunosPath, kunos.ToJson());

        var remote = new ChecksumsFile();
        remote.Tracks["shared"] = new TrackChecksum
        {
            Files = { ["models.ini"] = new ChecksumValue { MD5 = Md5("remote") } }
        };
        remote.Tracks["remote_only"] = new TrackChecksum
        {
            Files = { ["models.ini"] = new ChecksumValue { MD5 = Md5("remote-only") } }
        };
        string cachePath = Path.Combine(serverContent, "checksums_remote.json");
        string cachedJson = remote.ToJson();
        await File.WriteAllTextAsync(cachePath, cachedJson);
        using var provider = new ChecksumDataProvider(CreateServerConfiguration());

        ChecksumsFile loaded = await provider.LoadAsync();
        Assert.Multiple(() =>
        {
            Assert.That(loaded.Tracks["shared"].Files["models.ini"].MD5, Is.EqualTo(Md5("remote")));
            Assert.That(loaded.Tracks["shared"].Files["models.ini"].SHA256, Is.EqualTo(Sha256("ks-sha")));
            Assert.That(loaded.Tracks["remote_only"].Files["models.ini"].MD5, Is.EqualTo(Md5("remote-only")));
            Assert.That(loaded.Tracks["bundled_only"].Files["models.ini"].MD5, Is.EqualTo(Md5("bundled-only")));
        });
        Assert.That(File.Exists(cachePath), Is.True);
        var cachedRemote = ChecksumsFile.FromJson(await File.ReadAllTextAsync(cachePath));
        Assert.That(cachedRemote.Tracks["shared"].Files["models.ini"].MD5, Is.EqualTo(Md5("remote")));
        await provider.LoadAsync();
        Assert.That(await File.ReadAllTextAsync(cachePath), Is.EqualTo(cachedJson));
    }

    [Test]
    public async Task ProviderUsesBundledChecksumsWhenRemoteCacheIsEmpty()
    {
        using var directory = new TemporaryDirectory();
        using var currentDirectory = new CurrentDirectoryScope(directory.Path);
        string contentDirectory = Path.Combine(directory.Path, "content");
        Directory.CreateDirectory(contentDirectory);
        string kunosPath = Path.Combine(contentDirectory, "checksums_ks.json");
        var kunos = new ChecksumsFile();
        kunos.Tracks["ks_track"] = new TrackChecksum
        {
            Files = { ["models.ini"] = new ChecksumValue { MD5 = Md5("kunos") } }
        };
        string kunosJson = JsonSerializer.Serialize(kunos);
        await File.WriteAllTextAsync(kunosPath, kunosJson);
        await File.WriteAllTextAsync(Path.Combine(contentDirectory, "checksums_remote.json"), "{}");
        using var provider = new ChecksumDataProvider(CreateServerConfiguration());

        ChecksumsFile loaded = await provider.LoadAsync();
        string savedJson = await File.ReadAllTextAsync(kunosPath);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Tracks["ks_track"].Files["models.ini"].MD5, Is.EqualTo(Md5("kunos")));
            Assert.That(savedJson, Is.EqualTo(kunosJson));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ProviderReadsRemoteCacheWithoutReformatting(bool compact)
    {
        using var directory = new TemporaryDirectory();
        using var currentDirectory = new CurrentDirectoryScope(directory.Path);
        string contentDirectory = Path.Combine(directory.Path, "content");
        Directory.CreateDirectory(contentDirectory);
        string cachePath = Path.Combine(contentDirectory, "checksums_remote.json");

        var checksums = new ChecksumsFile();
        checksums.Tracks["test_track"] = new TrackChecksum
        {
            Files = { ["models.ini"] = ChecksumForContent("cached") }
        };
        var jsonObject = JsonNode.Parse(checksums.ToJson())!;
        string json = jsonObject.ToJsonString(new JsonSerializerOptions { WriteIndented = !compact });
        await File.WriteAllTextAsync(cachePath, json);

        using var provider = new ChecksumDataProvider(CreateServerConfiguration());

        ChecksumsFile loaded = await provider.LoadAsync();
        string savedJson = await File.ReadAllTextAsync(cachePath);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Tracks["test_track"].Files["models.ini"].MD5, Is.EqualTo(Md5("cached")));
            Assert.That(loaded.Tracks["test_track"].Files["models.ini"].SHA256, Is.EqualTo(Sha256("cached")));
            Assert.That(savedJson, Is.EqualTo(json));
        });
    }

    [Test]
    public async Task DisabledPreloadedChecksumsDoNotReadOrFetchSources()
    {
        using var directory = new TemporaryDirectory();
        using var currentDirectory = new CurrentDirectoryScope(directory.Path);
        using var provider = new ChecksumDataProvider(CreateServerConfiguration(disablePreloadedChecksums: true));

        ChecksumsFile loaded = await provider.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Tracks, Is.Empty);
            Assert.That(File.Exists(Path.Combine(directory.Path, "content", "checksums_ks.json")), Is.False);
            Assert.That(File.Exists(Path.Combine(directory.Path, "content", "checksums_remote.json")), Is.False);
        });
    }

    [Test]
    public void TrackLookupRequiresMatchingLayoutWhenTrackHasLayouts()
    {
        var checksums = new ChecksumsFile();
        checksums.Tracks["layout_track"] = new TrackChecksum
        {
            Files = { ["common.kn5"] = new ChecksumValue { MD5 = Md5("common") } },
            Layouts =
            {
                ["sprint"] = new TrackLayoutChecksum
                {
                    Files = { ["models_sprint.ini"] = new ChecksumValue { MD5 = Md5("layout") } }
                }
            }
        };
        checksums.Tracks["plain_track"] = new TrackChecksum
        {
            Files = { ["models.ini"] = new ChecksumValue { MD5 = Md5("plain") } }
        };

        Assert.That(checksums.TryGetTrack("layout_track", null, out _, out _), Is.False);
        Assert.That(checksums.TryGetTrack("layout_track", "missing", out _, out _), Is.False);
        Assert.That(checksums.TryGetTrack("layout_track", "sprint", out _, out var layout), Is.True);
        Assert.That(layout!.Files["models_sprint.ini"].MD5, Is.EqualTo(Md5("layout")));
        Assert.That(checksums.TryGetTrack("plain_track", null, out _, out _), Is.True);
        Assert.That(checksums.TryGetTrack("plain_track", "sprint", out _, out _), Is.False);
    }

    [Test]
    public void ChecksumJsonRoundTripsFixedSizeInlineByteArrays()
    {
        Assert.That(Unsafe.SizeOf<Md5Checksum>(), Is.EqualTo(16));
        Assert.That(Unsafe.SizeOf<Sha256Checksum>(), Is.EqualTo(32));

        var checksums = new ChecksumsFile();
        checksums.Cars["test_car"] = new CarChecksum
        {
            Files = { ["data.acd"] = ChecksumForContent("inline checksums") }
        };
        string json = checksums.ToJson();
        var jsonObject = JsonNode.Parse(json)!;
        var md5 = jsonObject["Cars"]!["test_car"]!["Files"]!["data.acd"]!["MD5"]!.AsArray();
        var sha256 = jsonObject["Cars"]!["test_car"]!["Files"]!["data.acd"]!["SHA256"]!.AsArray();

        Assert.That(md5.Count, Is.EqualTo(16));
        Assert.That(sha256.Count, Is.EqualTo(32));
        Assert.That(md5.All(value => value!.GetValueKind() == JsonValueKind.Number), Is.True);
        Assert.That(sha256.All(value => value!.GetValueKind() == JsonValueKind.Number), Is.True);
        Assert.That(json, Does.Match("\"MD5\": \\[[^\\r\\n]+\\]"));
        Assert.That(json, Does.Match("\"SHA256\": \\[[^\\r\\n]+\\]"));

        var loaded = ChecksumsFile.FromJson(json).Cars["test_car"].Files["data.acd"];
        Assert.That(loaded.MD5, Is.EqualTo(Md5("inline checksums")));
        Assert.That(loaded.SHA256, Is.EqualTo(Sha256("inline checksums")));
    }

    [Test]
    public void ChecksumJsonPreservesCaseInsensitivePropertiesAndDictionaries()
    {
        const string json = """
            {
              "tracks": {
                "TEST_TRACK": {
                  "files": {"MODELS.INI": {}},
                  "surfaces": {"SURFACES.INI": {}},
                  "layouts": {
                    "SPRINT": {
                      "files": {"MODELS_SPRINT.INI": {}},
                      "surfaces": {"SPRINT/DATA/SURFACES.INI": {}}
                    }
                  }
                }
              },
              "cars": {"TEST_CAR": {"files": {"DATA.ACD": {}}}},
              "other": {"SYSTEM/DATA/SURFACES.INI": {}}
            }
            """;

        var checksums = ChecksumsFile.FromJson(json);
        var track = checksums.Tracks["test_track"];
        var layout = track.Layouts["sprint"];
        Assert.Multiple(() =>
        {
            Assert.That(track.Files.ContainsKey("models.ini"), Is.True);
            Assert.That(track.Surfaces.ContainsKey("surfaces.ini"), Is.True);
            Assert.That(layout.Files.ContainsKey("models_sprint.ini"), Is.True);
            Assert.That(layout.Surfaces.ContainsKey("sprint/data/surfaces.ini"), Is.True);
            Assert.That(checksums.Cars["test_car"].Files.ContainsKey("data.acd"), Is.True);
            Assert.That(checksums.Other.ContainsKey("system/data/surfaces.ini"), Is.True);
        });
    }

    [Test]
    public void ChecksumJsonReadsAndOmitsNullDigests()
    {
        var checksums = ChecksumsFile.FromJson(
            """{"Cars":{"test_car":{"Files":{"data.acd":{"MD5":null,"SHA256":null}}}}}""");
        var checksum = checksums.Cars["test_car"].Files["data.acd"];
        var json = JsonNode.Parse(checksums.ToJson())!;

        Assert.Multiple(() =>
        {
            Assert.That(checksum.MD5, Is.Null);
            Assert.That(checksum.SHA256, Is.Null);
            Assert.That(json["Cars"]!["test_car"]!["Files"]!["data.acd"]!.AsObject(), Is.Empty);
            Assert.That(json["Cars"]!["test_car"]!.AsObject().ContainsKey("Info"), Is.False);
        });
    }

    [TestCase("MD5", "[1]")]
    [TestCase("MD5", "[256,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]")]
    [TestCase("MD5", "[-1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]")]
    [TestCase("MD5", "[1.5,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]")]
    [TestCase("MD5", "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]")]
    [TestCase("SHA256", "[1]")]
    public void ChecksumJsonRejectsInvalidDigestValues(string algorithm, string value)
    {
                string json = $$"""
                        {
                            "Cars": {
                                "test_car": {
                                    "Files": {
                                        "data.acd": { "{{algorithm}}": {{value}} }
                                    }
                                }
                            }
                        }
                        """;

        Assert.Throws<JsonException>(() => ChecksumsFile.FromJson(json));
    }

    [Test]
    public void UtilityReadsCaseInsensitiveAndNumericUiMetadata()
    {
        using var directory = new TemporaryDirectory();
        string trackDirectory = Path.Combine(directory.Path, "content", "tracks", "test_track");
        WriteFile(Path.Combine(trackDirectory, "models.ini"), "models");
        WriteFile(Path.Combine(trackDirectory, "ui", "ui_track.json"),
            """{"NAME":"Test Track","Version":"1.2","year":2020,"pitboxes":24}""");
        var kunos = new ChecksumsFile();
        var custom = new ChecksumsFile();

        ChecksumGenerator.UpdateFromLocalContent(kunos, custom, directory.Path);

        var info = custom.Tracks["test_track"].Info!;
        Assert.Multiple(() =>
        {
            Assert.That(info.Name, Is.EqualTo("Test Track"));
            Assert.That(info.Version, Is.EqualTo("1.2"));
            Assert.That(info.Year, Is.EqualTo("2020"));
            Assert.That(info.Pitboxes, Is.EqualTo("24"));
            Assert.That(info.Author, Is.Null);
        });
    }

    [TestCase("{")]
    [TestCase("[]")]
    [TestCase("null")]
    public void UtilityIgnoresUnreadableOptionalUiMetadata(string json)
    {
        using var directory = new TemporaryDirectory();
        string carDirectory = Path.Combine(directory.Path, "content", "cars", "test_car");
        WriteFile(Path.Combine(carDirectory, "data.acd"), "car data");
        WriteFile(Path.Combine(carDirectory, "ui", "ui_car.json"), json);
        var kunos = new ChecksumsFile();
        var custom = new ChecksumsFile();

        ChecksumGenerator.UpdateFromLocalContent(kunos, custom, directory.Path);

        Assert.Multiple(() =>
        {
            Assert.That(custom.Cars["test_car"].Files["data.acd"].MD5, Is.EqualTo(Md5("car data")));
            Assert.That(custom.Cars["test_car"].Info, Is.Null);
        });
    }

    [Test]
    public void UtilityUpdatesEligibleLocalContentAndPreservesUnmatchedEntries()
    {
        using var directory = new TemporaryDirectory();
        string cars = Path.Combine(directory.Path, "content", "cars");
        string tracks = Path.Combine(directory.Path, "content", "tracks");
        string plainTracks = Path.Combine(directory.Path, "content", "track");
        string carDirectory = Path.Combine(cars, "older_local_car");
        string equalVersionCarDirectory = Path.Combine(cars, "equal_version_car");
        string olderSameChecksumCarDirectory = Path.Combine(cars, "older_same_checksum_car");
        string unknownVersionCarDirectory = Path.Combine(cars, "unknown_version_car");
        string layoutTrack = Path.Combine(tracks, "layout_track");
        string plainTrack = Path.Combine(plainTracks, "plain_track");

        WriteFile(Path.Combine(carDirectory, "data.acd"), "new car data");
        WriteFile(Path.Combine(carDirectory, "ui", "ui_car.json"), """{"version":"2.0"}""");
        WriteFile(Path.Combine(equalVersionCarDirectory, "data.acd"), "equal version data");
        WriteFile(Path.Combine(equalVersionCarDirectory, "ui", "ui_car.json"), """{"version":"2.0"}""");
        WriteFile(Path.Combine(olderSameChecksumCarDirectory, "data.acd"), "same checksum data");
        WriteFile(Path.Combine(olderSameChecksumCarDirectory, "ui", "ui_car.json"), """{"version":"2.0"}""");
        WriteFile(Path.Combine(unknownVersionCarDirectory, "data.acd"), "unknown version data");
        WriteFile(Path.Combine(layoutTrack, "models_sprint.ini"), "layout model");
        WriteFile(Path.Combine(layoutTrack, "sprint", "data", "surfaces.ini"), "X SURFACE_0");
        WriteFile(Path.Combine(layoutTrack, "ui", "sprint", "ui_track.json"), """{"version":"2.0"}""");
        WriteFile(Path.Combine(plainTrack, "models.ini"), "plain model");
        WriteFile(Path.Combine(plainTrack, "ui", "ui_track.json"), """{"name":"Plain Track"}""");

        var kunosChecksums = new ChecksumsFile();
        var customChecksums = new ChecksumsFile();
        customChecksums.Cars["older_local_car"] = new CarChecksum
        {
            Info = new ChecksumInfo { Version = "3.0" },
            Files = { ["data.acd"] = new ChecksumValue { MD5 = Md5("old-older-local-car") } }
        };
        customChecksums.Cars["equal_version_car"] = new CarChecksum
        {
            Info = new ChecksumInfo { Version = "2.0" },
            Files = { ["data.acd"] = new ChecksumValue { MD5 = Md5("old-equal-version-car") } }
        };
        customChecksums.Cars["older_same_checksum_car"] = new CarChecksum
        {
            Info = new ChecksumInfo { Version = "3.0" },
            Files = { ["data.acd"] = ChecksumForContent("same checksum data") }
        };
        customChecksums.Cars["unknown_version_car"] = new CarChecksum
        {
            Info = new ChecksumInfo { Version = "9.0" },
            Files =
            {
                ["data.acd"] = new ChecksumValue { MD5 = Md5("old-unknown-version") },
                ["retained.acd"] = new ChecksumValue { MD5 = Md5("retained") }
            }
        };
        customChecksums.Cars["not_installed"] = new CarChecksum
        {
            Files = { ["data.acd"] = new ChecksumValue { MD5 = Md5("unmatched") } }
        };
        customChecksums.Tracks["layout_track"] = new TrackChecksum
        {
            Layouts =
            {
                ["sprint"] = new TrackLayoutChecksum
                {
                    Info = new ChecksumInfo { Version = "1.0" },
                    Files = { ["models_sprint.ini"] = new ChecksumValue { MD5 = Md5("old-layout") } }
                }
            }
        };
        customChecksums.Tracks["not_installed"] = new TrackChecksum
        {
            Files = { ["models.ini"] = new ChecksumValue { MD5 = Md5("unmatched-track") } }
        };

        ChecksumUpdateSummary summary = ChecksumGenerator.UpdateFromLocalContent(
            kunosChecksums, customChecksums, directory.Path);

        Assert.Multiple(() =>
        {
            Assert.That(customChecksums.Cars["older_local_car"].Files["data.acd"].MD5,
                Is.EqualTo(Md5("old-older-local-car")));
            Assert.That(customChecksums.Cars["equal_version_car"].Files["data.acd"].MD5,
                Is.Not.EqualTo(Md5("old-equal-version-car")));
            Assert.That(customChecksums.Cars["unknown_version_car"].Files["data.acd"].MD5,
                Is.Not.EqualTo(Md5("old-unknown-version")));
            Assert.That(customChecksums.Cars["unknown_version_car"].Files["retained.acd"].MD5,
                Is.EqualTo(Md5("retained")));
            Assert.That(customChecksums.Cars.ContainsKey("not_installed"), Is.True);
            Assert.That(customChecksums.Tracks["layout_track"].Layouts["sprint"].Files["models_sprint.ini"].MD5,
                Is.Not.EqualTo(Md5("old-layout")));
            Assert.That(customChecksums.Tracks["plain_track"].Info!.Name, Is.EqualTo("Plain Track"));
            Assert.That(customChecksums.Tracks["plain_track"].Files.ContainsKey("models.ini"), Is.True);
            Assert.That(customChecksums.Tracks.ContainsKey("not_installed"), Is.True);
            Assert.That(customChecksums.TryGetTrack("layout_track", null, out _, out _), Is.False);
            Assert.That(customChecksums.TryGetTrack("layout_track", "sprint", out _, out var layout), Is.True);
            Assert.That(layout!.Surfaces["sprint/data/surfaces.ini"].Csp!.MD5,
                Is.Not.EqualTo(layout.Surfaces["sprint/data/surfaces.ini"].Vanilla!.MD5));
            Assert.That(summary.SkippedChecksums.Select(checksum => checksum.ContentPath),
                Is.EqualTo(new[] { "content/cars/older_local_car/data.acd" }));
        });

        ChecksumUpdateSummary replaceSummary = ChecksumGenerator.UpdateFromLocalContent(
            kunosChecksums, customChecksums, directory.Path, replace: true);
        Assert.That(replaceSummary.SkippedChecksums, Is.Empty);
        Assert.That(customChecksums.Cars["older_local_car"].Files["data.acd"].MD5,
            Is.EqualTo(ChecksumForContent("new car data").MD5));
    }

    [Test]
    public async Task UtilityLoadsAndWritesKunosAndCustomChecksumDirectories()
    {
        using var directory = new TemporaryDirectory();
        string inputDirectory = Path.Combine(directory.Path, "input");
        string outputDirectory = Path.Combine(directory.Path, "output");
        Directory.CreateDirectory(inputDirectory);

        var inputKunos = new ChecksumsFile();
        inputKunos.Cars["ferrari_458"] = new CarChecksum
        {
            Files = { ["data.acd"] = new ChecksumValue { MD5 = Md5("old-ferrari") } }
        };
        inputKunos.Tracks["imola"] = new TrackChecksum
        {
            Files = { ["models.ini"] = new ChecksumValue { MD5 = Md5("old-imola") } }
        };
        inputKunos.Cars["not_installed"] = new CarChecksum
        {
            Files = { ["data.acd"] = new ChecksumValue { MD5 = Md5("preserved") } }
        };
        var inputCustom = new ChecksumsFile();
        inputCustom.Tracks["remote_only"] = new TrackChecksum
        {
            Files = { ["models.ini"] = new ChecksumValue { MD5 = Md5("preserved") } }
        };
        await File.WriteAllTextAsync(Path.Combine(inputDirectory, ChecksumDirectory.KunosFileName),
            JsonSerializer.Serialize(inputKunos));
        await File.WriteAllTextAsync(Path.Combine(inputDirectory, ChecksumDirectory.CustomFileName),
            JsonSerializer.Serialize(inputCustom));

        WriteFile(Path.Combine(directory.Path, "content", "cars", "ks_new_car", "data.acd"), "ks car");
        WriteFile(Path.Combine(directory.Path, "content", "cars", "ferrari_458", "data.acd"), "kunos car");
        WriteFile(Path.Combine(directory.Path, "content", "cars", "custom_car", "data.acd"), "custom car");
        WriteFile(Path.Combine(directory.Path, "content", "tracks", "ks_layout_track", "models_sprint.ini"), "layout");
        WriteFile(Path.Combine(directory.Path, "content", "tracks", "ks_layout_track", "sprint", "data", "surfaces.ini"),
            "SURFACE_0");
        WriteFile(Path.Combine(directory.Path, "content", "tracks", "custom_track", "models.ini"), "custom track");
        WriteFile(Path.Combine(directory.Path, "content", "track", "imola", "models.ini"), "kunos track");
        WriteFile(Path.Combine(directory.Path, "content", "track", "plain_custom_track", "models.ini"), "plain track");
        WriteFile(Path.Combine(directory.Path, "system", "data", "surfaces.ini"), "system surfaces");

        var (kunos, custom) = ChecksumDirectory.Load(inputDirectory);
        ChecksumUpdateSummary summary = ChecksumGenerator.UpdateFromLocalContent(
            kunos, custom, directory.Path);
        await ChecksumDirectory.SaveAsync(outputDirectory, kunos, custom);
        var (savedKunos, savedCustom) = ChecksumDirectory.Load(outputDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(summary.SkippedChecksums, Is.Empty);
            Assert.That(savedKunos.Cars.ContainsKey("ks_new_car"), Is.True);
            Assert.That(savedKunos.Cars["ferrari_458"].Files["data.acd"].MD5,
                Is.Not.EqualTo(Md5("old-ferrari")));
            Assert.That(savedKunos.Cars.ContainsKey("not_installed"), Is.True);
            Assert.That(savedCustom.Cars.ContainsKey("custom_car"), Is.True);
            Assert.That(savedKunos.Tracks.ContainsKey("ks_layout_track"), Is.True);
            Assert.That(savedKunos.Tracks.ContainsKey("imola"), Is.True);
            Assert.That(savedCustom.Tracks.ContainsKey("custom_track"), Is.True);
            Assert.That(savedCustom.Tracks.ContainsKey("plain_custom_track"), Is.True);
            Assert.That(savedCustom.Tracks.ContainsKey("remote_only"), Is.True);
            Assert.That(savedKunos.Other.ContainsKey("system/data/surfaces.ini"), Is.True);
            Assert.That(File.Exists(Path.Combine(outputDirectory, ChecksumDirectory.KunosFileName)), Is.True);
            Assert.That(File.Exists(Path.Combine(outputDirectory, ChecksumDirectory.CustomFileName)), Is.True);
        });
    }

    [Test]
    public void LocalChecksumIsNotReplacedByPreloadedChecksum()
    {
        var local = Md5("local car data");
        var collider = Md5("collider");
        var checksums = new Dictionary<string, byte[]>
        {
            ["content/cars/test_car/data.acd"] = local.ToArray()
        };

        ChecksumManager.AddPreloadedChecksum(
            checksums, @"content\cars\test_car\data.acd",
            Md5("preloaded car data"));
        ChecksumManager.AddPreloadedChecksum(
            checksums, "content/cars/test_car/collider.kn5",
            collider);

        Assert.That(checksums.Count, Is.EqualTo(2));
        Assert.That(checksums["content/cars/test_car/data.acd"], Is.EqualTo(local.ToArray()));
        Assert.That(checksums["content/cars/test_car/collider.kn5"], Is.EqualTo(collider.ToArray()));
    }

    [Test]
    public void SurfaceChecksumMergeKeepsPriorityAndFillsMissingDigests()
    {
        var higherPriority = new ChecksumsFile();
        var lowerPriority = new ChecksumsFile();
        higherPriority.Tracks["test_track"] = new TrackChecksum();
        lowerPriority.Tracks["test_track"] = new TrackChecksum();

        higherPriority.Tracks["test_track"].Surfaces["data/surfaces.ini"] = new SurfaceChecksumVariants
        {
            Vanilla = new ChecksumValue { MD5 = Md5("higher vanilla") },
            Csp = ChecksumForContent("higher CSP")
        };
        lowerPriority.Tracks["test_track"].Surfaces["data/surfaces.ini"] = new SurfaceChecksumVariants
        {
            Vanilla = ChecksumForContent("lower vanilla"),
            Csp = ChecksumForContent("lower CSP")
        };

        higherPriority.MergeMissingFrom(lowerPriority);

        var track = higherPriority.Tracks["test_track"];
        Assert.Multiple(() =>
        {
            Assert.That(track.Files, Is.Empty);
            Assert.That(track.Surfaces["data/surfaces.ini"].Vanilla!.MD5, Is.EqualTo(Md5("higher vanilla")));
            Assert.That(track.Surfaces["data/surfaces.ini"].Vanilla!.SHA256, Is.EqualTo(Sha256("lower vanilla")));
            Assert.That(track.Surfaces["data/surfaces.ini"].Csp!.MD5, Is.EqualTo(Md5("higher CSP")));
            Assert.That(track.Surfaces["data/surfaces.ini"].Csp!.SHA256, Is.EqualTo(Sha256("higher CSP")));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GlobalPreloadedChecksumsDoNotRequireATrackEntry(bool hasLocalFile)
    {
        using var directory = new TemporaryDirectory();
        using var currentDirectory = new CurrentDirectoryScope(directory.Path);
        var kunos = new ChecksumsFile();
        kunos.Other["system/data/surfaces.ini"] = ChecksumForContent("system surfaces");
        await ChecksumDirectory.SaveAsync("content", kunos, new ChecksumsFile());
        if (hasLocalFile)
            WriteFile(Path.Combine("system", "data", "surfaces.ini"), "local system surfaces");
        var configuration = CreateServerConfiguration();
        using var provider = new ChecksumDataProvider(configuration);
        var entryCarManager = new EntryCarManager(configuration, null!, null!, null!, null!);
        var manager = new ChecksumManager(configuration, entryCarManager, provider);

        await manager.InitializeAsync();

        Assert.That(manager.TrackChecksums["system/data/surfaces.ini"],
            Is.EqualTo(Md5(hasLocalFile ? "local system surfaces" : "system surfaces").ToArray()));
    }

    [Test]
    public async Task PreloadedRootSurfacesMatchTheExistingFileBasedProcess()
    {
        using var directory = new TemporaryDirectory();
        using var currentDirectory = new CurrentDirectoryScope(directory.Path);
        var configuration = CreateServerConfiguration();
        string trackName = configuration.CSPTrackOptions.Track;
        string trackDirectory = Path.Combine("content", "tracks", trackName);
        string modelsFile = string.IsNullOrEmpty(configuration.Server.TrackConfig)
            ? "models.ini"
            : $"models_{configuration.Server.TrackConfig}.ini";
        WriteFile(Path.Combine(trackDirectory, modelsFile), "models");
        WriteFile(Path.Combine(trackDirectory, "surfaces.ini"), "X SURFACE_0");

        var kunos = new ChecksumsFile();
        var custom = new ChecksumsFile();
        ChecksumGenerator.UpdateFromLocalContent(kunos, custom, directory.Path);
        var track = ChecksumsFile.Find(kunos.Tracks, trackName) ?? custom.Tracks[trackName];
        Assert.Multiple(() =>
        {
            Assert.That(track.Surfaces["surfaces.ini"].Vanilla!.MD5, Is.EqualTo(Md5("X SURFACE_0")));
            Assert.That(track.Surfaces["surfaces.ini"].Vanilla!.SHA256, Is.EqualTo(Sha256("X SURFACE_0")));
            Assert.That(track.Surfaces["surfaces.ini"].Csp!.MD5, Is.EqualTo(Md5("X CSPFACE_0")));
            Assert.That(track.Surfaces["surfaces.ini"].Csp!.SHA256, Is.EqualTo(Sha256("X CSPFACE_0")));
        });
        await ChecksumDirectory.SaveAsync("content", kunos, custom);
        using var provider = new ChecksumDataProvider(configuration);
        var entryCarManager = new EntryCarManager(configuration, null!, null!, null!, null!);
        var manager = new ChecksumManager(configuration, entryCarManager, provider);
        await manager.InitializeAsync();
        string virtualPath = $"content/tracks/{configuration.Server.Track}/surfaces.ini";
        byte[] localChecksum = manager.TrackChecksums[virtualPath];
        Directory.Delete(trackDirectory, recursive: true);

        await manager.InitializeAsync();

        Assert.That(manager.TrackChecksums[virtualPath], Is.EqualTo(localChecksum));
    }

    [Test]
    public void ChecksumJsonDeserializesWithoutAdditionalStructuralValidation()
    {
        const string json = """{"Tracks":null,"Cars":{"test_car":{"Files":null}},"Other":{"test":null}}""";

        var checksums = ChecksumsFile.FromJson(json);

        Assert.Multiple(() =>
        {
            Assert.That(checksums.Tracks, Is.Null);
            Assert.That(checksums.Cars["test_car"].Files, Is.Null);
            Assert.That(checksums.Other["test"], Is.Null);
        });
    }

    [TestCase("{")]
    [TestCase("null")]
    [TestCase("""{"Tracks":42}""")]
    [TestCase("""{"Cars":{"remote_car":{"Files":{"data.acd":{"MD5":[1]}}}}}""")]
    public async Task ProviderSkipsInvalidCacheAndPreservesOtherChecksumSources(string invalidJson)
    {
        using var directory = new TemporaryDirectory();
        using var currentDirectory = new CurrentDirectoryScope(directory.Path);
        string cachePath = Path.Combine("content", "checksums_remote.json");
        WriteFile(cachePath, invalidJson);
        var configuration = CreateServerConfiguration();
        var kunos = new ChecksumsFile();
        kunos.Tracks["bundled_track"] = new TrackChecksum
        {
            Files = { ["models.ini"] = ChecksumForContent("bundled models") }
        };
        kunos.Other["system/data/surfaces.ini"] = ChecksumForContent("bundled system surfaces");
        WriteFile(Path.Combine("content", "checksums_ks.json"), kunos.ToJson());
        WriteFile(Path.Combine("system", "data", "surfaces.ini"), "local system surfaces");
        using var provider = new ChecksumDataProvider(configuration);

        ChecksumsFile loaded = await provider.LoadAsync();
        var entryCarManager = new EntryCarManager(configuration, null!, null!, null!, null!);
        var manager = new ChecksumManager(configuration, entryCarManager, provider);
        await manager.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Tracks["bundled_track"].Files["models.ini"].MD5, Is.EqualTo(Md5("bundled models")));
            Assert.That(loaded.Cars, Is.Empty);
            Assert.That(manager.TrackChecksums["system/data/surfaces.ini"],
                Is.EqualTo(Md5("local system surfaces").ToArray()));
            Assert.That(File.ReadAllText(cachePath), Is.EqualTo(invalidJson));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void UnversionedReplacementDoesNotRetainThePreviousVersion(bool hasUiFile)
    {
        using var directory = new TemporaryDirectory();
        string carDirectory = Path.Combine(directory.Path, "content", "cars", "test_car");
        WriteFile(Path.Combine(carDirectory, "data.acd"), "unversioned data");
        if (hasUiFile)
            WriteFile(Path.Combine(carDirectory, "ui", "ui_car.json"), """{"name":"Test Car"}""");
        var custom = new ChecksumsFile();
        custom.Cars["test_car"] = new CarChecksum
        {
            Info = new ChecksumInfo { Version = "3.0", Author = "Original Author" },
            Files = { ["data.acd"] = ChecksumForContent("stored data") }
        };
        var kunos = new ChecksumsFile();

        ChecksumGenerator.UpdateFromLocalContent(kunos, custom, directory.Path);

        Assert.Multiple(() =>
        {
            Assert.That(custom.Cars["test_car"].Info!.Version, Is.Null);
            Assert.That(custom.Cars["test_car"].Info!.Author, Is.EqualTo("Original Author"));
            Assert.That(custom.Cars["test_car"].Files["data.acd"].MD5, Is.EqualTo(Md5("unversioned data")));
        });
        WriteFile(Path.Combine(carDirectory, "ui", "ui_car.json"), """{"version":"2.0"}""");
        WriteFile(Path.Combine(carDirectory, "data.acd"), "versioned data");

        var summary = ChecksumGenerator.UpdateFromLocalContent(kunos, custom, directory.Path);

        Assert.Multiple(() =>
        {
            Assert.That(summary.SkippedChecksums, Is.Empty);
            Assert.That(custom.Cars["test_car"].Info!.Version, Is.EqualTo("2.0"));
            Assert.That(custom.Cars["test_car"].Files["data.acd"].MD5, Is.EqualTo(Md5("versioned data")));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ForcedReplacementUpdatesCommonFilesAndPreservesAbsentLayouts(bool replace)
    {
        using var directory = new TemporaryDirectory();
        string trackDirectory = Path.Combine(directory.Path, "content", "tracks", "test_track");
        WriteFile(Path.Combine(trackDirectory, "common.kn5"), "local common model");
        WriteFile(Path.Combine(trackDirectory, "models_sprint.ini"), "local sprint models");
        WriteFile(Path.Combine(trackDirectory, "ui", "sprint", "ui_track.json"), """{"version":"1.0"}""");
        var custom = new ChecksumsFile();
        custom.Tracks["test_track"] = new TrackChecksum
        {
            Files = { ["common.kn5"] = ChecksumForContent("stored common model") },
            Layouts =
            {
                ["sprint"] = new TrackLayoutChecksum
                {
                    Info = new ChecksumInfo { Version = "3.0" },
                    Files = { ["models_sprint.ini"] = ChecksumForContent("stored sprint models") }
                },
                ["not_installed"] = new TrackLayoutChecksum
                {
                    Info = new ChecksumInfo { Version = "3.0" },
                    Files = { ["models_not_installed.ini"] = ChecksumForContent("preserved models") }
                }
            }
        };

        var summary = ChecksumGenerator.UpdateFromLocalContent(new ChecksumsFile(), custom, directory.Path, replace);

        Assert.Multiple(() =>
        {
            Assert.That(custom.Tracks["test_track"].Files["common.kn5"].MD5,
                Is.EqualTo(Md5(replace ? "local common model" : "stored common model")));
            Assert.That(custom.Tracks["test_track"].Layouts["not_installed"].Files["models_not_installed.ini"].MD5,
                Is.EqualTo(Md5("preserved models")));
            Assert.That(summary.SkippedChecksums.Count, Is.EqualTo(replace ? 0 : 2));
        });
    }

    [Test]
    public void SkippedLayoutChecksumsUseTrackRelativePaths()
    {
        using var directory = new TemporaryDirectory();
        string trackDirectory = Path.Combine(directory.Path, "content", "tracks", "test_track");
        WriteFile(Path.Combine(trackDirectory, "models_sprint.ini"), "stored model");
        WriteFile(Path.Combine(trackDirectory, "sprint", "data", "surfaces.ini"), "X SURFACE_0 stored");
        WriteFile(Path.Combine(trackDirectory, "ui", "sprint", "ui_track.json"), """{"version":"3.0"}""");
        var kunos = new ChecksumsFile();
        var custom = new ChecksumsFile();
        ChecksumGenerator.UpdateFromLocalContent(kunos, custom, directory.Path);
        WriteFile(Path.Combine(trackDirectory, "models_sprint.ini"), "local model");
        WriteFile(Path.Combine(trackDirectory, "sprint", "data", "surfaces.ini"), "X SURFACE_0 local");
        WriteFile(Path.Combine(trackDirectory, "ui", "sprint", "ui_track.json"), """{"version":"2.0"}""");

        var summary = ChecksumGenerator.UpdateFromLocalContent(kunos, custom, directory.Path);

        Assert.That(summary.SkippedChecksums.Select(checksum => checksum.ContentPath), Is.EqualTo(new[]
        {
            "content/tracks/test_track/models_sprint.ini",
            "content/tracks/test_track/sprint/data/surfaces.ini [vanilla]",
            "content/tracks/test_track/sprint/data/surfaces.ini [CSP]"
        }));
    }

    [Test]
    public void LargeFilesAreHashedWithoutFileSizedAllocations()
    {
        using var directory = new TemporaryDirectory();
        string trackDirectory = Path.Combine(directory.Path, "content", "tracks", "test_track");
        WriteFile(Path.Combine(trackDirectory, "models.ini"), "models");
        string modelPath = Path.Combine(trackDirectory, "large.kn5");
        using (var stream = File.Create(modelPath))
            stream.SetLength(16 * 1024 * 1024);
        byte[] expectedMd5;
        byte[] expectedSha256;
        using (var stream = File.OpenRead(modelPath))
        {
            expectedMd5 = MD5.HashData(stream);
            stream.Position = 0;
            expectedSha256 = SHA256.HashData(stream);
        }
        var kunos = new ChecksumsFile();
        var custom = new ChecksumsFile();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        ChecksumGenerator.UpdateFromLocalContent(kunos, custom, directory.Path);

        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var checksum = custom.Tracks["test_track"].Files["large.kn5"];
        Assert.Multiple(() =>
        {
            Assert.That(checksum.MD5!.Value.ToArray(), Is.EqualTo(expectedMd5));
            Assert.That(checksum.SHA256!.Value.ToArray(), Is.EqualTo(expectedSha256));
            Assert.That(allocatedBytes, Is.LessThan(1024 * 1024));
        });
    }

    [TestCase("checksums_ks.json")]
    [TestCase("checksums_remote.json")]
    public void ShippedChecksumFilesContainSingleLineFixedSizeByteArrays(string fileName)
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", fileName));
        var checksums = ChecksumsFile.FromJson(json);
        var jsonObject = JsonNode.Parse(json)!;
        var allProperties = EnumerateJsonProperties(jsonObject).ToArray();
        var properties = allProperties.Where(property => property.Key is "MD5" or "SHA256").ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(checksums.Tracks, Is.Not.Empty);
            Assert.That(checksums.Cars, Is.Not.Empty);
            Assert.That(properties, Is.Not.Empty);
            Assert.That(properties.All(property => property.Value is JsonArray values
                && values.Count == (property.Key == "MD5" ? Md5Checksum.Length : Sha256Checksum.Length)
                && values.All(value => value?.GetValueKind() == JsonValueKind.Number
                    && value.GetValue<int>() is >= 0 and <= 255)),
                Is.True);
            Assert.That(Regex.Matches(json, "\"(?:MD5|SHA256)\": \\[[^\\r\\n]+\\]").Count,
                Is.EqualTo(properties.Length));
            Assert.That(jsonObject["Cars"]!.AsObject()
                .All(car => car.Value!["Surfaces"] == null), Is.True);
            Assert.That(allProperties
                .Where(property => property.Key == "Files")
                .All(property => property.Value!.AsObject()
                    .All(file => !Path.GetFileName(file.Key.Replace('\\', '/'))
                        .Equals("surfaces.ini", StringComparison.OrdinalIgnoreCase))), Is.True);
        });
    }

    [Test]
    public async Task DisablingPreloadedChecksumsPreservesLocalFileChecksums()
    {
        using var directory = new TemporaryDirectory();
        using var currentDirectory = new CurrentDirectoryScope(directory.Path);
        var configuration = CreateServerConfiguration(disablePreloadedChecksums: true);
        string trackDirectory = Path.Combine("content", "tracks", configuration.CSPTrackOptions.Track);
        string modelsFile = string.IsNullOrEmpty(configuration.Server.TrackConfig)
            ? "models.ini"
            : $"models_{configuration.Server.TrackConfig}.ini";
        WriteFile(Path.Combine(trackDirectory, modelsFile), "local models");
        WriteFile(Path.Combine("system", "data", "surfaces.ini"), "local system surfaces");
        WriteFile(Path.Combine("content", "checksums_ks.json"), """{"Tracks":null}""");
        WriteFile(Path.Combine("content", "checksums_remote.json"), """{"Tracks":null}""");
        using var provider = new ChecksumDataProvider(configuration);
        var entryCarManager = new EntryCarManager(configuration, null!, null!, null!, null!);
        var manager = new ChecksumManager(configuration, entryCarManager, provider);

        await manager.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(manager.TrackChecksums["system/data/surfaces.ini"],
                Is.EqualTo(Md5("local system surfaces").ToArray()));
            Assert.That(manager.TrackChecksums[$"content/tracks/{configuration.Server.Track}/{modelsFile}"],
                Is.EqualTo(Md5("local models").ToArray()));
        });
    }

    [Test]
    public void CarsDoNotExposeOrSerializeSurfaceChecksums()
    {
        var checksums = new ChecksumsFile();
        checksums.Cars["test_car"] = new CarChecksum
        {
            Files = { ["data.acd"] = ChecksumForContent("car data") }
        };
        var json = JsonNode.Parse(checksums.ToJson())!;

        Assert.Multiple(() =>
        {
            Assert.That(typeof(CarChecksum).GetProperty("Surfaces"), Is.Null);
            Assert.That(json["Cars"]!["test_car"]!["Surfaces"], Is.Null);
        });
    }

    [Test]
    public void SurfaceChecksumsRoundTripForTracksAndLayouts()
    {
        var checksums = new ChecksumsFile();
        checksums.Tracks["test_track"] = new TrackChecksum
        {
            Surfaces =
            {
                ["surfaces.ini"] = new SurfaceChecksumVariants
                {
                    Vanilla = ChecksumForContent("track surface"),
                    Csp = ChecksumForContent("CSP track surface")
                }
            },
            Layouts =
            {
                ["sprint"] = new TrackLayoutChecksum
                {
                    Surfaces =
                    {
                        ["sprint/data/surfaces.ini"] = new SurfaceChecksumVariants
                        {
                            Vanilla = ChecksumForContent("layout surface"),
                            Csp = ChecksumForContent("CSP layout surface")
                        }
                    }
                }
            }
        };
        var loaded = ChecksumsFile.FromJson(checksums.ToJson());
        var track = loaded.Tracks["test_track"];
        var layout = track.Layouts["sprint"];
        Assert.Multiple(() =>
        {
            Assert.That(track.Files, Is.Empty);
            Assert.That(layout.Files, Is.Empty);
            Assert.That(track.Surfaces["surfaces.ini"].Vanilla!.MD5, Is.EqualTo(Md5("track surface")));
            Assert.That(track.Surfaces["surfaces.ini"].Csp!.SHA256, Is.EqualTo(Sha256("CSP track surface")));
            Assert.That(layout.Surfaces["sprint/data/surfaces.ini"].Vanilla!.MD5,
                Is.EqualTo(Md5("layout surface")));
            Assert.That(layout.Surfaces["sprint/data/surfaces.ini"].Csp!.SHA256,
                Is.EqualTo(Sha256("CSP layout surface")));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void UtilityGeneratesSurfacesOnlyInTheSurfaceCollections(bool hasLayouts)
    {
        using var directory = new TemporaryDirectory();
        string trackDirectory = Path.Combine(directory.Path, "content", "tracks", "test_track");
        WriteFile(Path.Combine(trackDirectory, hasLayouts ? "models_sprint.ini" : "models.ini"), "models");
        WriteFile(Path.Combine(trackDirectory, "surfaces.ini"), "root surfaces");
        string dataDirectory = hasLayouts
            ? Path.Combine(trackDirectory, "sprint", "data")
            : Path.Combine(trackDirectory, "data");
        WriteFile(Path.Combine(dataDirectory, "surfaces.ini"), "X SURFACE_0");
        WriteFile(Path.Combine(directory.Path, "content", "cars", "test_car", "data.acd"), "car data");
        var kunos = new ChecksumsFile();
        var custom = new ChecksumsFile();

        ChecksumGenerator.UpdateFromLocalContent(kunos, custom, directory.Path);

        var track = custom.Tracks["test_track"];
        TrackChecksumEntry dataEntry = hasLayouts ? track.Layouts["sprint"] : track;
        string dataSurfacePath = hasLayouts ? "sprint/data/surfaces.ini" : "data/surfaces.ini";
        Assert.Multiple(() =>
        {
            Assert.That(track.Surfaces.ContainsKey("surfaces.ini"), Is.True);
            Assert.That(dataEntry.Surfaces.ContainsKey(dataSurfacePath), Is.True);
            Assert.That(track.Files.ContainsKey("surfaces.ini"), Is.False);
            Assert.That(dataEntry.Files.ContainsKey(dataSurfacePath), Is.False);
            Assert.That(JsonNode.Parse(custom.ToJson())!["Cars"]!["test_car"]!["Surfaces"], Is.Null);
        });
    }

    private static IEnumerable<KeyValuePair<string, JsonNode?>> EnumerateJsonProperties(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var property in jsonObject)
            {
                yield return property;
                foreach (var nestedProperty in EnumerateJsonProperties(property.Value))
                    yield return nestedProperty;
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                foreach (var property in EnumerateJsonProperties(item))
                    yield return property;
            }
        }
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static ChecksumValue ChecksumForContent(string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        return new ChecksumValue
        {
            MD5 = Md5Checksum.FromBytes(MD5.HashData(bytes)),
            SHA256 = Sha256Checksum.FromBytes(SHA256.HashData(bytes))
        };
    }

    private static Md5Checksum Md5(string value)
    {
        return Md5Checksum.FromBytes(MD5.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static Sha256Checksum Sha256(string value)
    {
        return Sha256Checksum.FromBytes(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static ACServerConfiguration CreateServerConfiguration(bool disablePreloadedChecksums = false)
    {
        var locations = ConfigurationLocations.FromOptions(null, null, null);
        if (disablePreloadedChecksums)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(locations.ExtraCfgPath)!);
            new ACExtraConfiguration { DisablePreloadedChecksums = true }.ToFile(locations.ExtraCfgPath);
        }

        return new ACServerConfiguration(null, locations, false, false, null);
    }

    private sealed class CurrentDirectoryScope : IDisposable
    {
        private readonly string _previousDirectory;

        public CurrentDirectoryScope(string path)
        {
            _previousDirectory = Environment.CurrentDirectory;
            Environment.CurrentDirectory = path;
        }

        public void Dispose() => Environment.CurrentDirectory = _previousDirectory;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "AssettoServerTests", Guid.NewGuid().ToString("N"));

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
