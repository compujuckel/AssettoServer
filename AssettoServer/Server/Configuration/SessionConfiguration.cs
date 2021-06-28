using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Server.Configuration
{
    public class SessionConfiguration
    {
        public int Id { get; set; }
        public int Type => Id + 1;
        public string Name { get; set; }
        public int Time { get; set; }
        public int Laps { get; set; }
        public int WaitTime { get; set; }
        public bool IsOpen { get; set; }
        public DateTime StartTime { get; set; }
        public long StartTimeTicks { get; set; }
        public TimeSpan TimeLeft => StartTime.AddMinutes(Time) - DateTime.Now;
    }
}
