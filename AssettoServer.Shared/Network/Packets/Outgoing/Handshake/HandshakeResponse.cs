using AssettoServer.Shared.Model;
using AssettoServer.Shared.Network.Packets.Incoming;

namespace AssettoServer.Shared.Network.Packets.Outgoing.Handshake;

public class HandshakeResponse : IOutgoingNetworkPacket, IIncomingNetworkPacket
{
    public string ServerName = "";
    public ushort UdpPort;
    public byte RefreshRateHz;
    public string TrackName = "";
    public string TrackConfig = "";
    public string CarModel = "";
    public string CarSkin = "";
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
    public IEnumerable<Session> Sessions = null!;
    public Session CurrentSession = null!;
    public long SessionTime;
    public float TrackGrip;
    public byte SpawnPosition;
    public byte ChecksumCount;
    public IEnumerable<string>? ChecksumPaths;
    public string LegalTyres = "";
    public int RandomSeed;
    public int CurrentTime;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.NewCarConnection);
        writer.WriteUTF32String(ServerName);
        writer.Write<ushort>(UdpPort);
        writer.Write(RefreshRateHz);
        writer.WriteUTF8String(TrackName);
        writer.WriteUTF8String(TrackConfig);
        writer.WriteUTF8String(CarModel);
        writer.WriteUTF8String(CarSkin);
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
        
        foreach(var sessionConfiguration in Sessions)
        {
            writer.Write((byte)sessionConfiguration.Type);
            writer.Write((ushort)sessionConfiguration.Laps);
            writer.Write((ushort)sessionConfiguration.Time);
        }

        writer.WriteUTF8String(CurrentSession.Name);
        writer.Write((byte)CurrentSession.Id);
        writer.Write((byte)CurrentSession.Type);
        writer.Write((ushort)CurrentSession.Time);
        writer.Write((ushort)CurrentSession.Laps);

        writer.Write(TrackGrip);
        writer.Write(SessionId);
        writer.Write(SessionTime);

        writer.Write(ChecksumCount);
        if (ChecksumPaths != null)
            foreach (string path in ChecksumPaths)
                writer.WriteUTF8String(path);

        writer.WriteUTF8String(LegalTyres);
        writer.Write(RandomSeed);
        writer.Write(CurrentTime);
    }

    public void FromReader(PacketReader reader)
    {
        ServerName = reader.ReadUTF32String();
        UdpPort = reader.Read<ushort>();
        RefreshRateHz = reader.Read<byte>();
        TrackName = reader.ReadUTF8String();
        TrackConfig = reader.ReadUTF8String();
        CarModel = reader.ReadUTF8String();
        CarSkin = reader.ReadUTF8String();
        SunAngle = reader.Read<float>();
        AllowedTyresOutCount = reader.Read<short>();
        AllowTyreBlankets = reader.Read<bool>();
        TractionControlAllowed = reader.Read<byte>();
        ABSAllowed = reader.Read<byte>();
        StabilityAllowed = reader.Read<bool>();
        AutoClutchAllowed = reader.Read<bool>();
        JumpStartPenaltyMode = reader.Read<byte>();
        MechanicalDamageRate = reader.Read<float>();
        FuelConsumptionRate = reader.Read<float>();
        TyreConsumptionRate = reader.Read<float>();
        IsVirtualMirrorForced = reader.Read<bool>();
        MaxContactsPerKm = reader.Read<byte>();
        RaceOverTime = reader.Read<int>();
        ResultScreenTime = reader.Read<int>();
        HasExtraLap = reader.Read<bool>();
        IsGasPenaltyDisabled = reader.Read<bool>();
        PitWindowStart = reader.Read<short>();
        PitWindowEnd = reader.Read<short>();
        InvertedGridPositions = reader.Read<short>();
        SessionId = reader.Read<byte>();
        SessionCount = reader.Read<byte>();

        var sessions = new Session[SessionCount];

        for (int i = 0; i < sessions.Length; i++)
        {
            sessions[i] = new Session
            {
                Type = reader.Read<SessionType>(),
                Laps = reader.Read<ushort>(),
                Time = reader.Read<ushort>()
            };
        }

        Sessions = sessions;

        CurrentSession = new Session
        {
            Name = reader.ReadUTF8String(),
            Id = reader.Read<byte>(),
            Type = reader.Read<SessionType>(),
            Time = reader.Read<ushort>(),
            Laps = reader.Read<ushort>()
        };

        TrackGrip = reader.Read<float>();
        SessionId = reader.Read<byte>();
        SessionTime = reader.Read<long>();

        ChecksumCount = reader.Read<byte>();

        var checksumPaths = new string[ChecksumCount];

        for (int i = 0; i < checksumPaths.Length; i++)
        {
            checksumPaths[i] = reader.ReadUTF8String();
        }

        ChecksumPaths = checksumPaths;

        LegalTyres = reader.ReadUTF8String();
        RandomSeed = reader.Read<int>();
        CurrentTime = reader.Read<int>();
    }
}
