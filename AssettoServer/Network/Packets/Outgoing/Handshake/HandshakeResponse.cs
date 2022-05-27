using AssettoServer.Server.Configuration;
using System.Collections.Generic;
using AssettoServer.Server;

namespace AssettoServer.Network.Packets.Outgoing.Handshake
{
    public struct HandshakeResponse : IOutgoingNetworkPacket
    {
        public string ServerName;
        public ushort UdpPort;
        public byte RefreshRateHz;
        public string TrackName;
        public string TrackConfig;
        public string CarModel;
        public string CarSkin;
        public float SunAngle;
        public short AllowedTyresOutCount;
        public bool AllowTyreBlankets;
        public byte TractionControlAllowed;
        public byte ABSAllowed;
        public bool StabilityAllowed;
        public bool AutoClutchAllowed;
        public byte JumpStartPenaltyMode;
        public float MechanicalDamageRate;
        public float FuelConsumptionRate;
        public float TyreConsumptionRate;
        public bool IsVirtualMirrorForced;
        public byte MaxContactsPerKm;
        public int RaceOverTime;
        public int ResultScreenTime;
        public bool HasExtraLap;
        public bool IsGasPenaltyDisabled;
        public short PitWindowStart;
        public short PitWindowEnd;
        public short InvertedGridPositions;
        public byte SessionId;
        public byte SessionCount;
        public IEnumerable<SessionConfiguration> Sessions;
        public SessionState CurrentSession;
        public float TrackGrip;
        public byte SpawnPosition;
        public byte ChecksumCount;
        public IEnumerable<string> ChecksumPaths;
        public string LegalTyres;
        public int RandomSeed;
        public long CurrentTime;


        public readonly void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)ACServerProtocol.Handshake);
            writer.WriteUTF32String(ServerName);
            writer.Write<ushort>(UdpPort);
            writer.Write(RefreshRateHz);
            writer.WriteASCIIString(TrackName);
            writer.WriteASCIIString(TrackConfig);
            writer.WriteASCIIString(CarModel);
            writer.WriteASCIIString(CarSkin);
            writer.Write(SunAngle);
            writer.Write(AllowedTyresOutCount);
            writer.Write(AllowTyreBlankets);
            writer.Write(TractionControlAllowed);
            writer.Write(ABSAllowed);
            writer.Write(StabilityAllowed);
            writer.Write(AutoClutchAllowed);
            writer.Write(JumpStartPenaltyMode);
            writer.Write(MechanicalDamageRate);
            writer.Write(FuelConsumptionRate);
            writer.Write(TyreConsumptionRate);
            writer.Write(IsVirtualMirrorForced);
            writer.Write(MaxContactsPerKm);
            writer.Write(RaceOverTime);
            writer.Write(ResultScreenTime);
            writer.Write(HasExtraLap);
            writer.Write(IsGasPenaltyDisabled);
            writer.Write(PitWindowStart);
            writer.Write(PitWindowEnd);
            writer.Write(InvertedGridPositions);
            writer.Write(SessionId);
            writer.Write(SessionCount);

            foreach(SessionConfiguration sessionConfiguration in Sessions)
            {
                writer.Write((byte)sessionConfiguration.Type);
                writer.Write((ushort)sessionConfiguration.Laps);
                writer.Write((ushort)sessionConfiguration.Time);
            }

            writer.WriteASCIIString(CurrentSession.Configuration.Name);
            writer.Write((byte)CurrentSession.Configuration.Id);
            writer.Write((byte)CurrentSession.Configuration.Type);
            writer.Write((ushort)CurrentSession.Configuration.Time);
            writer.Write((ushort)CurrentSession.Configuration.Laps);

            writer.Write(TrackGrip);
            writer.Write(SessionId);
            writer.Write<long>(CurrentTime - CurrentSession.StartTimeMilliseconds);

            writer.Write(ChecksumCount);
            if (ChecksumPaths != null)
                foreach (string path in ChecksumPaths)
                    writer.WriteASCIIString(path);

            writer.WriteASCIIString(LegalTyres);
            writer.Write(RandomSeed);
            writer.Write<uint>((uint)CurrentTime);
        }
    }
}
