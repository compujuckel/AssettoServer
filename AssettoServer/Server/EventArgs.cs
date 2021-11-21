using System;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;

namespace AssettoServer.Server;

public class ClientEventArgs : EventArgs
{
    public ACTcpClient Client { get; init; }
}

public class ClientAuditEventArgs : EventArgs
{
    public ACTcpClient Client { get; init; }
    public KickReason Reason { get; init; }
    public string ReasonStr { get; init; }
    public ACTcpClient Admin { get; init; }
}

public class ChatEventArgs : EventArgs
{
    public ACTcpClient Client { get; init; }
    public string Message { get; init; }
}

public class ChatMessageEventArgs : EventArgs
{
    public ChatMessage ChatMessage { get; init; }
}