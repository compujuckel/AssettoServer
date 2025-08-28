using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

namespace AssettoServer.Server.Checksum;

public class ChecksumsFile
{
    public Dictionary<string, TrackChecksum> Tracks { get; set; } = [];
    public Dictionary<string, ChecksumFileList> Cars { get; set; } = [];
    public ChecksumFileList Other { get; set; } = [];

    public static ChecksumsFile FromFile(string path)
    {
        var deserializer = new DeserializerBuilder().Build();
        using var checksumsFile = File.OpenText(path);
        return deserializer.Deserialize<ChecksumsFile>(checksumsFile);
    }
}

/// <summary>
/// Tracks use the real path without /csp 
/// </summary>
public class TrackChecksum
{
    public Dictionary<string, TrackLayoutChecksum> Layouts { get; set; } = [];
    public TrackLayoutChecksum Default { get; set; } = new();
}

public class TrackLayoutChecksum
{
    public ChecksumFileList CSP { get; set; } = new();
    public ChecksumFileList Vanilla { get; set; } = new();
}

public class ChecksumFileList : Dictionary<string, ChecksumItem>;

public class ChecksumItem
{
    public string MD5 { get; set; }
    public string SHA256 { get; set; }
}
