using AssettoServer.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GroupStreetRacingPlugin
{
    public class StreetRace
    {
        //private readonly EntryCarManager _entryCarManager;
        private readonly SessionManager _sessionManager;
        private StreetRaceStatus _raceStatus;
        private EntryCar _raceOwner;
        private List<StreetRaceCar> _raceCars;

        public StreetRace(EntryCar raceOwner, SessionManager sessionManager)
        {
            _sessionManager = sessionManager;
            _raceOwner = raceOwner;
            _raceCars = new List<StreetRaceCar>();
            _raceStatus = StreetRaceStatus.Challenging;

            _raceCars.Add(new StreetRaceCar
            {
                Car = raceOwner,
                Health = 100
            });
        }
    }

    public struct StreetRaceCar
    {
        public EntryCar Car;
        public int Health;
        public int PositionInRace;
    }

    public enum StreetRaceStatus
    {
        Challenging,
        Starting,
        Started,
        Ended,
        Cancelled
    }
}
