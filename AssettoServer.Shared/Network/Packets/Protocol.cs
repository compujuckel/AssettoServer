namespace AssettoServer.Shared.Network.Packets;

public enum ACServerProtocol : byte
{
    P2PUpdate              = 0x0D,
    MandatoryPitUpdate     = 0x0E,
    Blacklisted            = 0x3B,
    WrongPassword          = 0x3C,
    RequestNewConnection   = 0x3D,
    NewCarConnection       = 0x3E,  // Sent as a response to a handshake
    CarListRequest         = 0x3F,
    CarList                = 0x40,
    ServerRunning          = 0x41,  // Sent when client tries to connect but no session is running => {Protocol;(uint)SessionTimeLeftMilliSeconds}
    UnsupportedProtocol    = 0x42,
    CleanExitDrive         = 0x43,  // Driver does clean disconnect
    Checksum               = 0x44,
    NoSlotsAvailable       = 0x45,
    PositionUpdate         = 0x46,
    Chat                   = 0x47,
    MegaPacket             = 0x48,
    LapCompleted           = 0x49,
    CurrentSessionUpdate   = 0x4A,
    RaceOver               = 0x4B,
    Pulse                  = 0x4C,  // Not exactly sure what this does
    CarDisconnected        = 0x4D,
    CarConnect             = 0x4E,
    SessionRequest         = 0x4F,
    TyreCompoundChange     = 0x50,
    WelcomeMessage         = 0x51,
    CarSetup               = 0x52,
    DrsZonesUpdate         = 0x53,
    SunAngleUpdate         = 0x54,
    DamageUpdate           = 0x56,
    RaceStart              = 0x57,
    SectorSplit            = 0x58,
    CarConnected           = 0x5A,
    DriverInfoUpdate       = 0x5B,
    VoteNextSession        = 0x64,
    VoteRestartSession     = 0x65,
    VoteKickUser           = 0x66,
    VoteQuorumNotReached   = 0x67,
    KickCar                = 0x68,
    SessionClosed          = 0x6E,
    AuthFailed             = 0x6F,
    BoPUpdate              = 0x70,  // Ballast and restrictor
    WeatherUpdate          = 0x78,
    ClientEvent            = 0x82,
    Extended               = 0xAB,
    LobbyCheck             = 0xC8,
    PingPong               = 0xF8,
    PingUpdate             = 0xF9,
}

public enum UdpPluginProtocol : byte
{
    NewSession             = 0x32,
    NewConnection          = 0x33,
    ClosedConnection       = 0x34,  // sent every time a car disconnects
    CarUpdate              = 0x35,
    CarInfo                = 0x36,  // sent as a response to GetCarInfo
    EndSession             = 0x37,
    Version                = 0x38,
    Chat                   = 0x39,
    ClientFirstUpdate      = 0x3A,
    SessionInfo            = 0x3B,  // sent as a response to GetSessionInfo
    Error                  = 0x3C,
    LapCompleted           = 0x49,
    ClientEvent            = 0x82,
    SetRealtimePosInterval = 0xC8,
    GetCarInfo             = 0xC9,
    SendChat               = 0xCA,  // sends chat to single car
    BroadcastChat          = 0xCB,  // sends chat to everyone
    GetSessionInfo         = 0xCC,
    SetSessionInfo         = 0xCD,
    KickUser               = 0xCE,
    NextSession            = 0xCF,
    RestartSession         = 0xD0,
    AdminCommand           = 0xD1,
}

public enum ClientEventType : byte
{
    CollisionWithCar       = 0x0A,
    CollisionWithEnv       = 0x0B,
    JumpStartPenalty       = 0x0C
}

public enum CSPMessageTypeTcp : byte
{
    SpectateCar          = 0x00,
    CarVisibilityUpdate  = 0x02,
    ClientMessage        = 0x03,
    SystemMessage        = 0x04,
    KickBanMessage       = 0x05,
}

public enum CSPMessageTypeUdp : byte
{
    WeatherUpdate        = 0x01,
    CustomUpdate         = 0x03,
    ClientMessage        = 0x05,
}
