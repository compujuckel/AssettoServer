using System.Collections.Generic;

namespace AssettoServer.Server.Checksum;

public class ChecksumsFile
{
    public Dictionary<string, TrackChecksum> Tracks { get; init; }
    public Dictionary<string, ChecksumFileList> Cars { get; init; }
    public ChecksumFileList Other { get; init; }
}

public class TrackChecksum
{
    public Dictionary<string, TrackLayoutChecksum>? Layouts { get; init; }
    public TrackLayoutChecksum? Default { get; init; }
}

public class TrackLayoutChecksum
{
    public ChecksumFileList CSP { get; init; }
    public ChecksumFileList Vanilla { get; init; }
}

public class ChecksumFileList : Dictionary<string, ChecksumItem>;

public class ChecksumItem
{
    public string MD5 { get; init; }
    public string SHA256 { get; init; }
}
