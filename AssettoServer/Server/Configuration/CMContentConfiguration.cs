using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Server.Configuration
{
    public class CMContentConfiguration
    {
        public Dictionary<string, CMContentEntryCar> Cars { get; set; }
        public CMContentEntryVersionized Track { get; set; }
    }

    public class CMContentEntryCar : CMContentEntryVersionized
    {
        public Dictionary<string, CMContentEntry> Skins { get; set; }
    }

    public class CMContentEntryVersionized : CMContentEntry
    {
        public string Version { get; set; }
    }

    public class CMContentEntry
    {
        public string Url { get; set; }
    }

}
