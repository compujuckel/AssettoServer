using System;
using System.ComponentModel;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;

namespace AssettoServer.Server;

public delegate void EventHandler<TSender, TArgs>(TSender sender, TArgs args) where TArgs : EventArgs;
public delegate void EventHandlerIn<TSender, TArg>(TSender sender, in TArg args) where TArg : struct;

public class ClientAuditEventArgs : EventArgs
{
    public KickReason Reason { get; init; }
    public string? ReasonStr { get; init; }
    public ACTcpClient? Admin { get; init; }
}

public class ClientHandshakeEventArgs : CancelEventArgs
{
    public HandshakeRequest HandshakeRequest { get; init; }

    public CancelTypeEnum CancelType { get; set; }
    public string? AuthFailedReason { get; set; }

    public enum CancelTypeEnum
    {
        Blacklisted,
        AuthFailed
    }
}

public class ChatEventArgs : CancelEventArgs
{
    public string Message { get; }

    public ChatEventArgs(string message)
    {
        Message = message;
    }
}

public class ChatMessageEventArgs : EventArgs
{
    public ChatMessage ChatMessage { get; init; }
}
