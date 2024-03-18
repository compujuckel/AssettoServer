using System.Collections.Generic;
using AssettoServer.Utils;
using IniParser;
using IniParser.Model;
using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration.Kunos;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class EntryList
{
    [IniSection("CAR")] public IReadOnlyList<Entry> Cars { get; init; } = new List<Entry>();
    
    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
    public class Entry
    {
        [IniField("MODEL")] public string Model { get; init; } = "";
        [IniField("SKIN")] public string? Skin { get; init; }
        [IniField("SPECTATOR_MODE")] public int SpectatorMode { get; init; }
        [IniField("BALLAST")] public float Ballast { get; init; }
        [IniField("RESTRICTOR")] public int Restrictor { get; init; }
        [IniField("DRIVERNAME")] public string? DriverName { get; init; }
        [IniField("TEAM")] public string? Team { get; init; }        
        [IniField("FIXED_SETUP")] public string? FixedSetup { get; init; } = null;
        [IniField("GUID")] public string Guid { get; init; } = "";
        [IniField("AI")] public AiMode AiMode { get; internal set; } = AiMode.None;
    }
    
    public static EntryList FromFile(string path)
    {
        var parser = new FileIniDataParser();
        IniData data = parser.ReadFile(path);
        return data.DeserializeObject<EntryList>();
    }
}
