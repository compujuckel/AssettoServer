using System;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Tcp;

namespace AssettoServer.Server;

public class CSPLuaMessageType
{
    public int MessageType { get; }
    public Func<IIncomingNetworkPacket> FactoryMethod { get; }
    public Action<ACTcpClient, IIncomingNetworkPacket> Handler { get; }

    public CSPLuaMessageType(int messageType, Func<IIncomingNetworkPacket> factoryMethod, Action<ACTcpClient, IIncomingNetworkPacket> handler)
    {
        MessageType = messageType;
        FactoryMethod = factoryMethod;
        Handler = handler;
    }
}