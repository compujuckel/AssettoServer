using System.Collections.Generic;
using AssettoServer.Shared.Model;
using AssettoServer.Utils;
using IniParser;
using IniParser.Model;
using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration.Kunos;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class CarSetups
{
    public Dictionary<string, CarSetup> Setups { get; set; } = new();

    [UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
    public class CarSetup
    {
        [IniField("CAR", "MODEL")] public string CarModel { get; init; }
        [IniField("__EXT_PATCH", "VERSION")] public string CspVersion { get; init; }
        public Dictionary<string, float> Settings { get; init; } = new();
    }

    public static CarSetup FromFile(string path)
    {
        var parser = new FileIniDataParser();
        IniData data = parser.ReadFile(path);
        var setup = data.DeserializeObject<CarSetup>();

        foreach (var setting in data.Sections)
        {
            if (!setting.Keys.ContainsKey("VALUE")
                || string.IsNullOrEmpty(setting.SectionName)) continue;
            var name = setting.SectionName!;
            var val = float.Parse(setting.Keys.GetKeyData("VALUE").Value);

            setup.Settings[name] = val;
        }

        return setup;
    }
}
