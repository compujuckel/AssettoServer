using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Server.Configuration
{
    public class CMContentConfiguration
    {
        public Dictionary<string, CMContentEntry> Cars { get; set; }
        public CMContentEntry Track { get; set; }
    }

    public class CMContentEntry
    {
        public string Version { get; set; }
        public string Url { get; set; }

    }

}
