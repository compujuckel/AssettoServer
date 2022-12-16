using System.Collections.Generic;
using AssettoServer.Network.Packets;
using AssettoServer.Network.Tcp;

namespace AssettoServer.Server;

public delegate void ClientMessageHandler(ACTcpClient sender, ref PacketReader reader);

public class CSPClientMessageTypeManager
{
    internal IReadOnlyDictionary<uint, ClientMessageHandler> MessageTypes => _types;

    private readonly Dictionary<uint, ClientMessageHandler> _types = new();

    public void RegisterClientMessageType(uint type, ClientMessageHandler handler)
    {
        _types.Add(type, handler);
    }
}
