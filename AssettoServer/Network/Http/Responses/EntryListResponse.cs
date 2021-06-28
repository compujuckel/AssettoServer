using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Http
{
    public class EntryListResponseCar
    {
        public string Model { get; internal set; }
        public string Skin { get; internal set; }
        public string DriverName { get; set; }
        public string DriverTeam { get; set; }
        public bool IsRequestedGuid { get; set; }
        public bool IsEntryList { get; set; }
        public bool IsConnected { get; set; }
    }

    public class EntryListResponse
    {
        public IEnumerable<string> Features { get; set; }
        public IEnumerable<EntryListResponseCar> Cars { get; set; }
    }
}
