using System;
using System.Collections.Generic;
using AssettoServer.Network.ClientMessages;
using AssettoServer.Network.Tcp;
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Shared.Network.Packets.Shared;

namespace AssettoServer.Server;

public class CSPClientMessageTypeManager
{
    internal IReadOnlyDictionary<uint, Action<ACTcpClient, PacketReader>> MessageTypes => _types;
    internal IReadOnlyDictionary<CSPClientMessageType, Action<ACTcpClient, PacketReader>> RawMessageTypes => _rawTypes;

    private readonly Dictionary<uint, Action<ACTcpClient, PacketReader>> _types = new();
    private readonly Dictionary<CSPClientMessageType, Action<ACTcpClient, PacketReader>> _rawTypes = new();

    public void RegisterClientMessageType(uint type, Action<ACTcpClient, PacketReader> handler)
    {
        _types.Add(type, handler);
    }
    
    public void RegisterRawClientMessageType(CSPClientMessageType type, Action<ACTcpClient, PacketReader> handler)
    {
        _rawTypes.Add(type, handler);
    }

    public void RegisterOnlineEvent<TEvent>(Action<ACTcpClient, TEvent> handler) where TEvent : OnlineEvent<TEvent>, new()
    {
        _types.Add(OnlineEvent<TEvent>.PacketType, (sender, reader) =>
        {
            var packet = reader.ReadPacket<TEvent>();
            handler(sender, packet);
        });
    }
}
