using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Server.Configuration
{
    public class DynamicTrackConfiguration
    {
        public bool Enabled { get; internal set; }
        public float BaseGrip { get; internal set; }
        public float TotalLapCount { get; internal set; }
        public float GripPerLap { get; internal set; }
    }
}
