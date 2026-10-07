# ChecksumUtils

`ChecksumUtils` updates checksum JSON files from a local Assetto Corsa installation. Release archives include the tool under `utils/`; it requires the .NET 11 runtime.

## Usage

Run the compiled executable from a source checkout to read and update both checksum datasets in place:

```powershell
.\out-win-x64\utils\ChecksumUtils.exe --assetto "C:\Path\To\assettocorsa" --input .\AssettoServer\Assets --output .\AssettoServer\Assets
```

```bash
./out-linux-x64/utils/ChecksumUtils --assetto "/path/to/assettocorsa" --input ./AssettoServer/Assets --output ./AssettoServer/Assets
```

`--assetto` and `--output` are required. `--input` optionally loads `checksums_ks.json` and `checksums_remote.json` from a directory; missing files start as empty datasets. Both files are written to `--output`, creating the directory if needed.

For a release archive:

```powershell
.\utils\ChecksumUtils.exe --assetto "C:\Path\To\assettocorsa" --input .\content --output .\content
```

```bash
./utils/ChecksumUtils --assetto "/path/to/assettocorsa" --input ./content --output ./content
```

## Publishing

```powershell
dotnet publish .\ChecksumUtils\ChecksumUtils.csproj --runtime win-x64 -c Release
```

```bash
dotnet publish ./ChecksumUtils/ChecksumUtils.csproj --runtime linux-x64 -c Release
```

The published executables use the source-checkout paths shown above. `--runtime linux-arm64` produces `out-linux-arm64/utils/ChecksumUtils`.

## Dataset updates

Cars are discovered under `content/cars/<car>/`. Tracks are discovered from `content/tracks/<track>/models_<layout>.ini` for layouts or `models.ini` for no-layout tracks; the latter also supports `content/track/<track>/`.

Content beginning with `ks_`, listed in `KunosCarsWithoutKsPrefix` or `KunosTracksWithoutKsPrefix`, or already present in the input Kunos dataset goes to `checksums_ks.json`; other content goes to `checksums_remote.json`. Missing entries are added. Content, layouts, and file checksums absent locally are preserved.

Without `--replace`, entries are updated when the local version is at least the stored version, or either version is missing or unparseable. A missing local version clears the stored version; other unavailable UI fields are retained. `--replace` ignores version checks and also updates shared track files when some stored layouts are not installed locally.

UI metadata is optional: cars use `ui/ui_car.json`; tracks use `ui/<layout>/ui_track.json` or `ui/ui_track.json`, relative to their content directory. Missing files or fields do not prevent checksum generation.

The final summary lists skipped files with differing MD5 or SHA256 values, showing both stored and local values. Paths are relative to the track or car root.

## JSON format

The utility writes lowercase hexadecimal strings: 32 characters for MD5 and 64 for SHA256. Hex strings are decoded directly into fixed-size inline arrays of 16 and 32 bytes, without allocating strings or digest arrays. Uppercase input is also accepted; numeric JSON arrays are not. JSON is deserialized directly without a separate schema-validation pass; whitespace formatting is not significant.

Shared track files belong to the track entry and layout-specific files to `Layouts`. A track with layouts requires an explicit layout; it is not used as a no-layout track.

Only tracks and layouts have `Surfaces`, where `surfaces.ini` is stored with `Vanilla` and `Csp` variants rather than in `Files`. Cars contain `Info` and `Files`. The generator calculates both surface variants; the server uses vanilla for root-level `surfaces.ini`.

The global `system/data/surfaces.ini` checksum belongs to `Other` and applies even if the active track is absent from the datasets.

Example checksum data:

```json
{
  "Tracks": {
    "shuto": {
      "Layouts": {
        "main_layout": {
          "Info": {
            "Name": "Main Layout",
            "Version": "1.2"
          },
          "Files": {
            "models_main_layout.ini": {
              "MD5": "0123456789abcdef0123456789abcdef",
              "SHA256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
            }
          },
          "Surfaces": {
            "main_layout/data/surfaces.ini": {
              "Vanilla": {
                "MD5": "0123456789abcdef0123456789abcdef"
              },
              "Csp": {
                "MD5": "fedcba9876543210fedcba9876543210"
              }
            }
          }
        }
      }
    }
  },
  "Cars": {
    "custom_car": {
      "Info": {
        "Name": "Custom Car",
        "Version": "1.0",
        "Brand": "Example"
      },
      "Files": {
        "data.acd": {
          "MD5": "0123456789abcdef0123456789abcdef"
        }
      }
    }
  }
}
```

## Server checksum data

The server supplements checksums generated from files in the server directory with preloaded data. The per-file source priority is:

1. Local file checksums.
2. Fetched checksums cached at `content/checksums_remote.json`.
3. Bundled Kunos checksums at `content/checksums_ks.json`.

The [remote dataset](https://raw.githubusercontent.com/compujuckel/AssettoServer/master/AssettoServer/Assets/checksums_remote.json) is fetched only when the cache is missing and cached without reformatting. Fetch, read, and deserialization failures are logged without stopping startup; other sources remain available. Failed downloads are not cached and are retried on the next startup. Existing caches are never rewritten or automatically re-fetched; correct or remove an unreadable cache to use remote data again.

The server logs and skips unusable collections, entries, and individual digests while retaining valid checksums from both files. Invalid JSON syntax prevents reading that file, but does not prevent loading the other source. Utility deserialization remains strict.

Preloading is enabled by default. Set `EnablePreloadedChecksums: false` in `extra_cfg.yml` to use only local file checksums.
