using System.Collections.Generic;
using AssettoServer.Shared.Model;
using AssettoServer.Utils;
using IniParser;
using IniParser.Model;
using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration.Kunos;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class DrsZones
{
    [IniSection("ZONE")] public IReadOnlyList<DrsZone> Zones { get; init; } = new List<DrsZone>();
    
    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
    public class DrsZone : IDrsZone
    {
        [IniField("DETECTION")] public float Detection { get; init; }
        [IniField("START")] public float Start { get; init; }
        [IniField("END")] public float End { get; init; }
    }
    
    public static DrsZones FromFile(string path)
    {
        var parser = new FileIniDataParser();
        IniData data = parser.ReadFile(path);
        return data.DeserializeObject<DrsZones>();
    }
}
