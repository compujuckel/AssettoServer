using System;
using AssettoServer.Network.Packets.Incoming;
using AssettoServer.Network.Tcp;

namespace AssettoServer.Server;

public class CSPLuaMessageType
{
    public int MessageType { get; init; }
    public Func<IIncomingNetworkPacket> FactoryMethod { get; init; }
    public Action<ACTcpClient, IIncomingNetworkPacket> Handler { get; init; }
}