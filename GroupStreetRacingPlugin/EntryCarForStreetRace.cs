using AssettoServer.Network.Packets;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Server;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GroupStreetRacingPlugin
{
    public class EntryCarForStreetRace
    {
        public readonly EntryCar EntryCar;
        private int LightFlashCount { get; set; }
        private long LastLightFlashTime { get; set; }
        private long LastRaceChallengeTime { get; set; }

        public bool IsHazardsOn = false;
        public int CarHealth { get; set; }

        private GroupStreetRacing _groupStreetRacing;
        //internal Race? CurrentRace { get; set; }

        public EntryCarForStreetRace(EntryCar entryCar, GroupStreetRacing groupStreetRacing)
        {
            EntryCar = entryCar;            
            _groupStreetRacing = groupStreetRacing;
            EntryCar.PositionUpdateReceived += _entryCar_PositionUpdateReceived;
            EntryCar.ResetInvoked += _entryCar_ResetInvoked;
            CarHealth = 100;
        }

        private void Client_Collision(AssettoServer.Network.Tcp.ACTcpClient sender, CollisionEventArgs args)
        {                        
            if (sender.SessionId == EntryCar.SessionId)
            {
                var newHealth = CarHealth - (int)args.Speed;
                Log.Debug("Health Before: " + CarHealth + ", after: " + newHealth);
                CarHealth = Math.Clamp(newHealth, 0, 100);
                if (IsHazardsOn)
                    _groupStreetRacing.UpdateHazardList();
            }
            
            //EventType = (byte)(args.TargetCar == null ? ClientEventType.CollisionWithEnv : ClientEventType.CollisionWithCar),
            //SessionId = sender.SessionId,
            //TargetSessionId = args.TargetCar?.SessionId,
            //Speed = args.Speed,
            //WorldPosition = args.Position,
            //RelPosition = args.RelPosition,
        }

        private void _entryCar_ResetInvoked(EntryCar sender, EventArgs args)
        {
            //throw new NotImplementedException();
        }

        private void _entryCar_PositionUpdateReceived(EntryCar sender, in AssettoServer.Network.Packets.Incoming.PositionUpdateIn args)
        {
            //throw new NotImplementedException();
            //if ((this._entryCar.Status.StatusFlag & 8192) == null && (positionUpdate.StatusFlag & 8192) != null)
            if ((EntryCar.Status.StatusFlag & CarStatusFlags.HazardsOn) != 0)
            {
                if (IsHazardsOn) return;
                CarHealth = 100;
                IsHazardsOn = true;
                if (EntryCar.Client != null && EntryCar.Client.HasSentFirstUpdate)
                    EntryCar.Client.Collision += Client_Collision;
                _groupStreetRacing.AddCarFromHazardList(this);
            } else
            {
                if (!IsHazardsOn) return;
                IsHazardsOn = false;
                if (EntryCar.Client != null && EntryCar.Client.HasSentFirstUpdate)
                    EntryCar.Client.Collision -= Client_Collision;
                _groupStreetRacing.RemoveCarFromHazardList(this);
            }
        }
    }
}
