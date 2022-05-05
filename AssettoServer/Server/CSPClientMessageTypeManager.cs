using System;
using System.Collections.Generic;
using AssettoServer.Network.Packets;
using AssettoServer.Network.Tcp;

namespace AssettoServer.Server;

public class CSPClientMessageTypeManager
{
    internal IReadOnlyDictionary<uint, Action<ACTcpClient, PacketReader>> MessageTypes => _types;

    private readonly Dictionary<uint, Action<ACTcpClient, PacketReader>> _types = new();

    public void RegisterClientMessageType(uint type, Action<ACTcpClient, PacketReader> handler)
    {
        _types.Add(type, handler);
    }
}
