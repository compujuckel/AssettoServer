namespace AssettoServer.Network.Rcon;

public enum RconProtocolIn
{
    ExecCommand = 2,
    Auth = 3
}

public enum RconProtocolOut
{
    ResponseValue = 0,
    AuthResponse = 2
}
